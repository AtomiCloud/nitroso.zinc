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
)
  : IBookingRepository
{
  public async Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search)
  {
    try
    {
      logger.LogInformation("Searching for Booking with '{@Search}'", search.ToJson());
      var query = db.Bookings.AsQueryable();
      if (!string.IsNullOrWhiteSpace(search.UserId))
        query = query.Where(x => x.UserId == search.UserId);

      if (search.Date != null) query = query.Where(x => x.Date == search.Date);

      if (search.Time != null) query = query.Where(x => x.Time == search.Time);

      if (search.Status != null) query = query.Where(x => x.Status == (byte)search.Status);

      if (search.Direction != null)
      {
        var d = search.Direction?.ToData();
        query = query.Where(x => x.Direction == d);
      }

      var result = await query
        .Skip(search.Skip)
        .Take(search.Limit)
        .ToArrayAsync();

      return result
        .Select(x => x.ToPrincipal())
        .ToResult();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed search for Book with {@Search}", search.ToJson());
      return e;
    }
  }

  public async Task<Result<Booking?>> Get(string? userId, Guid id)
  {
    try
    {
      logger.LogInformation("Retrieving Booking with Id '{Id}' under User '{UserId}'", id, userId);
      var booking = await db
        .Bookings
        .Where(
          x => x.Id == id
               &&
               (userId == null || x.UserId == userId)
        )
        .Include(x => x.User)
        .Include(x => x.Transaction)
        .ThenInclude(x => x.Wallet)
        .FirstOrDefaultAsync();
      return booking?.ToDomain();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed retrieving Booking with Id '{Id}' under User '{UserId}'", id, userId);
      return e;
    }
  }

  public async Task<Result<BookingPrincipal>> Create(string userId, Guid transactionId, BookingRecord record)
  {
    try
    {
      logger.LogInformation("Creating Booking: {@Record}", record.ToJson());

      var data = new BookingData { UserId = userId, TransactionId = transactionId, };
      data = data.UpdateData(record);


      var r = db.Bookings.Add(data);
      await db.SaveChangesAsync();

      await cdc.Add("create");
      return r.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e,
        "Failed to create Booking under User '{UserId}': {@Record} due to conflict with existing record",
        userId, record.ToJson());

      return new EntityConflict(
          $"Failed to create Booking under User '{userId}' due to conflicting with existing record",
          typeof(BookingPrincipal))
        .ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create Booking under User '{UserId}': {@Record}",
        userId, record.ToJson());
      return e;
    }
  }

  public async Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingStatus? status,
    BookingRecord? record, BookingComplete? complete)
  {
    try
    {
      logger.LogInformation("Updating Booking '{Id}' under User '{UserId}' with: {@Record}, {@Status} and {@Complete}",
        id, userId,
        record?.ToJson() ?? "null", status?.ToJson() ?? "null", complete?.ToJson() ?? "null");
      var v1 = await db.Bookings
        .Where(x =>
          x.Id == id
          &&
          (userId == null || x.UserId == userId)
        )
        .FirstOrDefaultAsync();

      if (v1 == null) return (BookingPrincipal?)null;

      if (record is not null) v1 = v1.UpdateData(record);
      if (status is not null) v1 = v1.UpdateData(status);
      if (complete is not null) v1 = v1.UpdateData(complete);

      var updated = db.Bookings.Update(v1);
      await db.SaveChangesAsync();
      await cdc.Add("update");
      return updated.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e,
        "Failed to create Booking '{Id}' under User '{UserId}': {@Record}, {@Status} and {@Complete} due to conflict with existing record",
        id, userId, record?.ToJson() ?? "null", status?.ToJson() ?? "null", complete?.ToJson() ?? "null");
      return new EntityConflict(
          $"Failed to create Booking '{id}' under User '{userId}' due to conflicting with existing record",
          typeof(BookingPrincipal))
        .ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create Passenger '{Id}' under User {UserId} '{@Record}, {@Status} and {@Complete}'",
        id, userId, record?.ToJson() ?? "null", status?.ToJson() ?? "null", complete?.ToJson() ?? "null");
      return e;
    }
  }

  public async Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly time)
  {
    try
    {
      logger.LogInformation("Reserve Booking '{direction}' '{Date}' '{Time}'", direction, date, time);

      var v1 = await db.Bookings
        .Where(x =>
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
      logger.LogError(e, "Failed to reserve Booking '{direction}' '{Date}' '{Time}'", direction,
        date, time);
      return e;
    }
  }

  public async Task<Result<Unit?>> Delete(string? userId, Guid id)
  {
    try
    {
      logger.LogInformation("Deleting Booking '{Id}' under User '{UserId}'", id, userId);
      var a = await db.Bookings
        .Where(x => x.Id == id && (userId == null || x.UserId == userId))
        .FirstOrDefaultAsync();
      if (a == null) return (Unit?)null;

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

  public async Task<Result<IEnumerable<BookingCount>>> Count(DateOnly date, TimeOnly time)
  {
    try
    {
      logger.LogInformation("Get booking count from {Date} and {Time}...", date, time);

      var polls = await db.Bookings
        .Where(x =>
          (x.Date > date || (x.Date == date && x.Time >= time))
          &&
          x.Status == (int)BookStatus.Pending
        )
        .GroupBy(x => new { x.Date, x.Time, x.Direction })
        .Select(group =>
          new BookingCount
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
