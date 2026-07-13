using System.Data;
using App.Error.V1;
using App.Modules.Timings.Data;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Booking;
using Domain.Timings;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace App.Modules.Bookings.Data;

public class BookingRepository(
  MainDbContext db,
  ILogger<BookingRepository> logger,
  IBookingCdcRepository cdc
) : IBookingRepository
{
  // shared by Search and SearchCount so the count always agrees with the page
  private static IQueryable<BookingData> ApplyFilters(
    IQueryable<BookingData> query,
    BookingSearch search
  )
  {
    if (!string.IsNullOrWhiteSpace(search.UserId))
      query = query.Where(x => x.UserId == search.UserId);

    if (search.Date != null)
      query = query.Where(x => x.Date == search.Date);

    if (search.Time != null)
      query = query.Where(x => x.Time == search.Time);

    if (search.Status != null)
      query = query.Where(x => x.Status == (byte)search.Status);

    if (search.Direction != null)
    {
      var d = search.Direction?.ToData();
      query = query.Where(x => x.Direction == d);
    }

    if (!string.IsNullOrWhiteSpace(search.PassportNumber))
      query = query.Where(x => x.Passenger.PassportNumber == search.PassportNumber);

    if (!string.IsNullOrWhiteSpace(search.PassengerName))
    {
      // fuzzy = case-insensitive contains; escape ILIKE wildcards so a
      // literal % or _ in the input cannot widen the match
      var pattern =
        "%"
        + search
          .PassengerName.Replace(@"\", @"\\")
          .Replace("%", @"\%")
          .Replace("_", @"\_")
        + "%";
      query = query.Where(x => EF.Functions.ILike(x.Passenger.FullName, pattern, @"\"));
    }

    if (search.BuyingBefore != null)
      query = query.Where(x => x.LastBuyingAt != null && x.LastBuyingAt < search.BuyingBefore);

    // ticket-repair worklist: Completed bookings that carry no ticket file
    // reference. DB-level only — storage is deliberately never probed here
    if (search.MissingTicket == true)
      query = query.Where(x => x.Status == (byte)BookStatus.Completed && x.Ticket == null);

    return query;
  }

  public async Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search)
  {
    try
    {
      logger.LogInformation("Searching for Booking with '{@Search}'", search.ToJson());
      var query = ApplyFilters(db.Bookings.AsQueryable(), search);

      // Id tiebreaker everywhere: Skip/Take pagination needs a stable total
      // order, and none of the sort keys are unique
      var sorted = search.Sort switch
      {
        BookingSort.Timing => query.OrderBy(x => x.Date).ThenBy(x => x.Time).ThenBy(x => x.Id),
        BookingSort.PassengerName => query
          .OrderBy(x => x.Passenger.FullName)
          .ThenBy(x => x.Id),
        BookingSort.PassportNumber => query
          .OrderBy(x => x.Passenger.PassportNumber)
          .ThenBy(x => x.Id),
        // purchase instant, newest first
        BookingSort.BuyTime => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
        // fulfilment instant, newest first; unfulfilled (null) bookings last
        BookingSort.FulfilTime => query
          .OrderBy(x => x.CompletedAt == null)
          .ThenByDescending(x => x.CompletedAt)
          .ThenBy(x => x.Id),
        _ => query.OrderByDescending(x => x.Date).ThenBy(x => x.Id),
      };

      var result = await sorted.Skip(search.Skip).Take(search.Limit).ToArrayAsync();

      return result.Select(x => x.ToPrincipal()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed search for Book with {@Search}", search.ToJson());
      return e;
    }
  }

  public async Task<Result<int>> SearchCount(BookingSearch search)
  {
    try
    {
      return await ApplyFilters(db.Bookings.AsQueryable(), search).CountAsync();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed counting Bookings with {@Search}", search.ToJson());
      return e;
    }
  }

  // statuses still waiting for the buyer form the timeslot's queue,
  // processed priority-first, then oldest-first
  private static bool IsQueued(byte status) =>
    status
      is (byte)BookStatus.Pending
        or (byte)BookStatus.Buying
        or (byte)BookStatus.Recovering;

  public async Task<Result<BookingQueuePosition?>> QueuePosition(string? userId, Guid id)
  {
    try
    {
      var b = await db
        .Bookings.Where(x => x.Id == id && (userId == null || x.UserId == userId))
        .FirstOrDefaultAsync();
      if (b == null)
        return (BookingQueuePosition?)null;

      if (!IsQueued(b.Status))
        return new BookingQueuePosition
        {
          Status = (BookStatus)b.Status,
          Position = null,
          Total = null,
        };

      var slot = db.Bookings.Where(x =>
        x.Date == b.Date
        && x.Time == b.Time
        && x.Direction == b.Direction
        && (
          x.Status == (byte)BookStatus.Pending
          || x.Status == (byte)BookStatus.Buying
          || x.Status == (byte)BookStatus.Recovering
        )
      );
      // the buyer works priority-first (earliest boost wins), then
      // oldest-first (Id as the deterministic tiebreak) — the predicate must
      // mirror Reserve()'s ordering exactly or the shown position lies
      var ahead = await slot.Where(BookingQueue.AheadOf(b)).CountAsync();
      var total = await slot.CountAsync();

      return new BookingQueuePosition
      {
        Status = (BookStatus)b.Status,
        Position = ahead + 1,
        Total = total,
      };
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to compute queue position for Booking '{Id}'", id);
      return e;
    }
  }

  // refresh-on-read state for the booking_stats materialized view: one
  // timestamp per pod; staleness is checked in-process so most reads cost
  // nothing extra
  private static DateTime statsRefreshedAtUtc = DateTime.MinValue;

  private static readonly TimeSpan StatsMaxStaleness = TimeSpan.FromMinutes(5);

  // Refresh booking_stats when this pod last saw a refresh > 5 minutes ago.
  // pg_try_advisory_lock makes concurrent pods/requests skip instead of
  // stampeding, and any failure falls through to reading the (possibly
  // slightly stale) view — a refresh must never block reads. REFRESH
  // CONCURRENTLY cannot run inside a transaction, so this uses a raw command
  // and is skipped when an ambient transaction exists (the stats read path
  // never starts one).
  private async Task RefreshStatsViewIfStale()
  {
    if (DateTime.UtcNow - statsRefreshedAtUtc < StatsMaxStaleness)
      return;
    // writes use ambient TransactionScope (TransactionManager), which never
    // surfaces on CurrentTransaction — check both so REFRESH CONCURRENTLY
    // can never be dragged into a transaction
    if (
      db.Database.CurrentTransaction != null
      || global::System.Transactions.Transaction.Current != null
    )
      return;

    try
    {
      var conn = db.Database.GetDbConnection();
      if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

      await using (var tryLock = conn.CreateCommand())
      {
        tryLock.CommandText =
          "SELECT pg_try_advisory_lock(hashtext('booking_stats_refresh'))";
        if (await tryLock.ExecuteScalarAsync() is not true)
          return; // someone else is refreshing; read the current view
      }

      try
      {
        await using var refresh = conn.CreateCommand();
        refresh.CommandText =
          $"REFRESH MATERIALIZED VIEW CONCURRENTLY {BookingStatsView.Name}";
        await refresh.ExecuteNonQueryAsync();
        statsRefreshedAtUtc = DateTime.UtcNow;
      }
      finally
      {
        await using var unlock = conn.CreateCommand();
        unlock.CommandText = "SELECT pg_advisory_unlock(hashtext('booking_stats_refresh'))";
        await unlock.ExecuteNonQueryAsync();
      }
    }
    catch (Exception e)
    {
      logger.LogWarning(e, "Failed to refresh {View}; serving stale stats", BookingStatsView.Name);
    }
  }

  public async Task<Result<IEnumerable<BookingStatRow>>> Stats(BookingStatsQuery statsQuery)
  {
    try
    {
      // Managed Postgres occasionally terminates established connections
      // during a handoff (57P01). A stats read is side-effect free, so retry it
      // on a fresh connection instead of turning that brief event into a 500.
      // Keep this local rather than enabling global EF retries: write paths use
      // ambient TransactionScope transactions and need different semantics.
      var retry = new NpgsqlRetryingExecutionStrategy(
        db,
        // one initial attempt plus two retries
        maxRetryCount: 2,
        maxRetryDelay: TimeSpan.FromSeconds(1),
        errorCodesToAdd: ["57P01", "57P02", "57P03"]
      );

      var rows = await retry.ExecuteAsync(async () =>
      {
        await RefreshStatsViewIfStale();

        var query = db.BookingStats.AsQueryable();
        if (statsQuery.After != null)
          query = query.Where(x => x.Date >= statsQuery.After);
        if (statsQuery.Before != null)
          query = query.Where(x => x.Date <= statsQuery.Before);

        // the view's grain keeps the travel date (for the range filter above);
        // the response aggregates it away, so re-group DB-side on everything
        // else and sum the precomputed counts
        return await query
          .GroupBy(x => new
          {
            x.DayOfWeek,
            x.Time,
            x.Direction,
            x.Priority,
            x.LeadBucket,
            x.DemandBucket,
            x.DeliveryBucket,
          })
          .Select(g => new
          {
            g.Key,
            Total = g.Sum(x => x.Total),
            Completed = g.Sum(x => x.Completed),
            Refunded = g.Sum(x => x.Refunded),
            Cancelled = g.Sum(x => x.Cancelled),
            Terminated = g.Sum(x => x.Terminated),
            Other = g.Sum(x => x.Other),
          })
          .ToArrayAsync();
      });

      return rows
        .Select(x => new BookingStatRow
        {
          DayOfWeek = (DayOfWeek)x.Key.DayOfWeek,
          Time = x.Key.Time,
          Direction = x.Key.Direction.ToTrainDirection(),
          Priority = x.Key.Priority,
          Bucket = x.Key.LeadBucket,
          DemandBucket = x.Key.DemandBucket,
          // '' is the view's NULL sentinel (keeps the unique index total)
          DeliveryBucket = x.Key.DeliveryBucket == "" ? null : x.Key.DeliveryBucket,
          Total = x.Total,
          Completed = x.Completed,
          Refunded = x.Refunded,
          Cancelled = x.Cancelled,
          Terminated = x.Terminated,
          Other = x.Other,
        })
        .ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to compute booking stats");
      return e;
    }
  }


  public async Task<Result<IEnumerable<BookingPrincipal>>> RefundList(DateOnly date, TimeOnly time)
  {
    try
    {
      logger.LogInformation(
        "Searching for tickets to refund at {@DateOnly} {@TimeOnly}",
        date,
        time
      );

      var query = db
        .Bookings.Where(x =>
          (x.Date < date || (x.Date == date && x.Time <= time))
          && x.Status == (int)BookStatus.Pending
        )
        .AsQueryable();

      var result = await query.ToArrayAsync();

      return result.Select(x => x.ToPrincipal()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed search for tickets to refund at {@DateOnly} {@TimeOnly}",
        date,
        time
      );
      return e;
    }
  }

  public async Task<Result<Booking?>> Get(string? userId, Guid id)
  {
    try
    {
      logger.LogInformation("Retrieving Booking with Id '{Id}' under User '{UserId}'", id, userId);
      var booking = await db
        .Bookings.Where(x => x.Id == id && (userId == null || x.UserId == userId))
        .Include(x => x.User)
        .Include(x => x.Transaction)
        .ThenInclude(x => x.Wallet)
        .FirstOrDefaultAsync();
      return booking?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed retrieving Booking with Id '{Id}' under User '{UserId}'",
        id,
        userId
      );
      return e;
    }
  }

  public async Task<Result<BookingPrincipal>> Create(
    string userId,
    Guid transactionId,
    BookingRecord record,
    BookingPriceBreakdown? breakdown
  )
  {
    try
    {
      logger.LogInformation("Creating Booking: {@Record}", record.ToJson());

      var data = new BookingData { UserId = userId, TransactionId = transactionId };
      data = data.UpdateData(record);
      data.PriceBreakdown = breakdown?.ToData();

      var r = db.Bookings.Add(data);
      await db.SaveChangesAsync();

      await cdc.Add("create");
      return r.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(
        e,
        "Failed to create Booking under User '{UserId}': {@Record} due to conflict with existing record",
        userId,
        record.ToJson()
      );

      return new EntityConflict(
        $"Failed to create Booking under User '{userId}' due to conflicting with existing record",
        typeof(BookingPrincipal)
      ).ToException();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to create Booking under User '{UserId}': {@Record}",
        userId,
        record.ToJson()
      );
      return e;
    }
  }

  public async Task<Result<BookingPrincipal?>> Update(
    string? userId,
    Guid id,
    BookingStatus? status,
    BookingRecord? record,
    BookingComplete? complete
  )
  {
    try
    {
      logger.LogInformation(
        "Updating Booking '{Id}' under User '{UserId}' with: {@Record}, {@Status} and {@Complete}",
        id,
        userId,
        record?.ToJson() ?? "null",
        status?.ToJson() ?? "null",
        complete?.ToJson() ?? "null"
      );
      var v1 = await db
        .Bookings.Where(x => x.Id == id && (userId == null || x.UserId == userId))
        .FirstOrDefaultAsync();

      if (v1 == null)
        return (BookingPrincipal?)null;

      if (record is not null)
        v1 = v1.UpdateData(record);
      if (status is not null)
        v1 = v1.UpdateData(status);
      if (complete is not null)
        v1 = v1.UpdateData(complete);

      var updated = db.Bookings.Update(v1);
      await db.SaveChangesAsync();
      return updated.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(
        e,
        "Failed to create Booking '{Id}' under User '{UserId}': {@Record}, {@Status} and {@Complete} due to conflict with existing record",
        id,
        userId,
        record?.ToJson() ?? "null",
        status?.ToJson() ?? "null",
        complete?.ToJson() ?? "null"
      );
      return new EntityConflict(
        $"Failed to create Booking '{id}' under User '{userId}' due to conflicting with existing record",
        typeof(BookingPrincipal)
      ).ToException();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to create Passenger '{Id}' under User {UserId} '{@Record}, {@Status} and {@Complete}'",
        id,
        userId,
        record?.ToJson() ?? "null",
        status?.ToJson() ?? "null",
        complete?.ToJson() ?? "null"
      );
      return e;
    }
  }

  public async Task<Result<BookingPrincipal?>> Reserve(
    TrainDirection direction,
    DateOnly date,
    TimeOnly time
  )
  {
    try
    {
      logger.LogInformation(
        "Reserve Booking '{direction}' '{Date}' '{Time}'",
        direction,
        date,
        time
      );

      // priority first (earliest boost wins; NULL PrioritizedAt = pre-audit
      // boost, falls back to CreatedAt), then oldest-first (Id tiebreak) —
      // must mirror BookingQueue.AheadOf, which QueuePosition uses to count
      // "ahead"
      var v1 = await db
        .Bookings.Where(x =>
          x.Direction == direction.ToData()
          && x.Date == date
          && x.Time == time
          && x.Status == (int)BookStatus.Pending
        )
        .OrderByDescending(x => x.Priority)
        .ThenBy(x => x.PrioritizedAt ?? x.CreatedAt)
        .ThenBy(x => x.CreatedAt)
        .ThenBy(x => x.Id)
        .FirstOrDefaultAsync();
      return v1?.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to reserve Booking '{direction}' '{Date}' '{Time}'",
        direction,
        date,
        time
      );
      return e;
    }
  }

  public async Task<Result<BookingPrincipal?>> Prioritize(
    string? userId,
    Guid id,
    decimal? fee,
    string? grantedBy
  )
  {
    try
    {
      logger.LogInformation(
        "Prioritizing Booking '{Id}' under User '{UserId}' with fee {Fee} granted by '{GrantedBy}'",
        id,
        userId,
        fee,
        grantedBy
      );
      var v1 = await db
        .Bookings.Where(x => x.Id == id && (userId == null || x.UserId == userId))
        .FirstOrDefaultAsync();
      if (v1 == null)
        return (BookingPrincipal?)null;

      v1.Priority = true;
      v1.PriorityFee = fee;
      v1.PrioritizedAt = DateTime.UtcNow;
      v1.PrioritizedBy = grantedBy;
      await db.SaveChangesAsync();
      return v1.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to prioritize Booking '{Id}' under User '{UserId}'", id, userId);
      return e;
    }
  }

  public async Task<Result<BookingPrincipal?>> IncrementRecoveryRetries(Guid id)
  {
    try
    {
      logger.LogInformation("Incrementing recovery retries for Booking '{Id}'", id);
      var v1 = await db.Bookings.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (v1 == null)
        return (BookingPrincipal?)null;

      v1.RecoveryRetries += 1;
      await db.SaveChangesAsync();
      return v1.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to increment recovery retries for Booking '{Id}'", id);
      return e;
    }
  }

  public async Task<Result<Unit?>> Delete(string? userId, Guid id)
  {
    try
    {
      logger.LogInformation("Deleting Booking '{Id}' under User '{UserId}'", id, userId);
      var a = await db
        .Bookings.Where(x => x.Id == id && (userId == null || x.UserId == userId))
        .FirstOrDefaultAsync();
      if (a == null)
        return (Unit?)null;

      db.Bookings.Remove(a);
      await db.SaveChangesAsync();
      await cdc.Add("delete");
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete Bookings '{Id}' under User '{UserId}'", id, userId);
      return e;
    }
  }

  public async Task<Result<IEnumerable<BookingCount>>> Count(
    DateOnly date,
    TimeOnly time,
    DateOnly? filterDate,
    TrainDirection? filterDirection
  )
  {
    try
    {
      logger.LogInformation("Get booking count from {Date} and {Time}...", date, time);

      var query = db
        .Bookings.Where(x =>
          (x.Date > date || (x.Date == date && x.Time >= time))
          && x.Status == (int)BookStatus.Pending
        )
        .AsQueryable();

      if (filterDate != null)
        query = query.Where(x => x.Date == filterDate);
      if (filterDirection != null)
      {
        var fd = filterDirection?.ToData();
        query = query.Where(x => x.Direction == fd);
      }

      var polls = await query
        .GroupBy(x => new
        {
          x.Date,
          x.Time,
          x.Direction,
        })
        .Select(group => new BookingCount
        {
          Date = group.Key.Date,
          Time = group.Key.Time,
          Direction = group.Key.Direction.ToTrainDirection(),
          // total stays for old argon clients; new clients read the split
          TicketsNeeded = group.Count(),
          Priority = group.Count(x => x.Priority),
          Normal = group.Count(x => !x.Priority),
        })
        .ToArrayAsync();
      return polls;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to poll Booking Count from {@FromDate} {@FromTime}", date, time);
      return e;
    }
  }
}
