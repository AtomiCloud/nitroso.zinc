using App.Error.V1;
using App.Modules.Timings.Data;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Booking;
using Domain.Timings;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;

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
  // processed oldest-first
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
      // the buyer works oldest-first, so everyone who booked earlier (Id as
      // the deterministic tiebreak) is ahead of this booking
      var ahead = await slot
        .Where(x => x.CreatedAt < b.CreatedAt || (x.CreatedAt == b.CreatedAt && x.Id < b.Id))
        .CountAsync();
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

  public async Task<Result<IEnumerable<BookingStatRow>>> Stats(BookingStatsQuery statsQuery)
  {
    try
    {
      var query = db.Bookings.AsQueryable();
      if (statsQuery.After != null)
        query = query.Where(x => x.Date >= statsQuery.After);
      if (statsQuery.Before != null)
        query = query.Where(x => x.Date <= statsQuery.Before);

      // minimal projection; grouping happens in memory because the lead-time
      // bucket is computed from two columns and SQL GROUP BY can't help
      var slices = await query
        .Select(x => new
        {
          x.Date,
          x.Time,
          x.Direction,
          x.Status,
          x.CreatedAt,
        })
        .ToArrayAsync();

      var rows = slices
        .GroupBy(x => new
        {
          x.Date.DayOfWeek,
          x.Time,
          x.Direction,
          Bucket = BookingStats.LeadTimeBucket(x.Date, x.Time, x.CreatedAt),
        })
        .Select(g => new BookingStatRow
        {
          DayOfWeek = g.Key.DayOfWeek,
          Time = g.Key.Time,
          Direction = g.Key.Direction.ToTrainDirection(),
          Bucket = g.Key.Bucket,
          Total = g.Count(),
          Completed = g.Count(x => x.Status == (byte)BookStatus.Completed),
          Refunded = g.Count(x => x.Status == (byte)BookStatus.Refunded),
          Cancelled = g.Count(x => x.Status == (byte)BookStatus.Cancelled),
          Terminated = g.Count(x => x.Status == (byte)BookStatus.Terminated),
          Other = g.Count(x =>
            x.Status != (byte)BookStatus.Completed
            && x.Status != (byte)BookStatus.Refunded
            && x.Status != (byte)BookStatus.Cancelled
            && x.Status != (byte)BookStatus.Terminated
          ),
        })
        .ToArray();

      return rows.AsEnumerable().ToResult();
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
    BookingRecord record
  )
  {
    try
    {
      logger.LogInformation("Creating Booking: {@Record}", record.ToJson());

      var data = new BookingData { UserId = userId, TransactionId = transactionId };
      data = data.UpdateData(record);

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

      var v1 = await db
        .Bookings.Where(x =>
          x.Direction == direction.ToData()
          && x.Date == date
          && x.Time == time
          && x.Status == (int)BookStatus.Pending
        )
        .OrderBy(x => x.CreatedAt)
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
          TicketsNeeded = group.Count(),
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
