using App.Modules.Common;
using App.StartUp.Database;
using App.StartUp.Options;
using App.StartUp.Registry;
using CSharp_Result;
using Domain.Booking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace App.Modules.Bookings.Data;

public static class BookingKtmbCheckMapper
{
  public static BookingKtmbCheck ToDomain(this BookingKtmbCheckData d) =>
    new()
    {
      Id = d.Id,
      BookingId = d.BookingId,
      Status = (KtmbCheckStatus)d.Status,
      RequestedAt = d.RequestedAt,
      RequestedBy = d.RequestedBy,
      CheckedAt = d.CheckedAt,
      KtmbBookingNo = d.KtmbBookingNo,
      KtmbTicketNo = d.KtmbTicketNo,
      ClaimedByBookingId = d.ClaimedByBookingId,
      Error = d.Error,
    };
}

public class BookingKtmbCheckRepository(
  MainDbContext db,
  ILogger<BookingKtmbCheckRepository> logger
) : IBookingKtmbCheckRepository
{
  private const int ErrorMax = 1024;

  public async Task<Result<BookingKtmbCheck>> Create(
    Guid bookingId,
    string? requestedBy,
    DateTime requestedAt
  )
  {
    try
    {
      var data = new BookingKtmbCheckData
      {
        BookingId = bookingId,
        Status = (byte)KtmbCheckStatus.Pending,
        RequestedAt = requestedAt,
        RequestedBy = requestedBy,
      };
      db.BookingKtmbChecks.Add(data);
      await db.SaveChangesAsync();
      return data.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create KTMB check for Booking '{BookingId}'", bookingId);
      return e;
    }
  }

  public async Task<Result<BookingKtmbCheck?>> Latest(Guid bookingId)
  {
    try
    {
      var data = await db
        .BookingKtmbChecks.Where(x => x.BookingId == bookingId)
        .OrderByDescending(x => x.RequestedAt)
        .FirstOrDefaultAsync();
      return data?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to get latest KTMB check for Booking '{BookingId}'", bookingId);
      return e;
    }
  }

  public async Task<Result<BookingKtmbCheck?>> Get(Guid checkId)
  {
    try
    {
      var data = await db.BookingKtmbChecks.Where(x => x.Id == checkId).FirstOrDefaultAsync();
      return data?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to get KTMB check '{CheckId}'", checkId);
      return e;
    }
  }

  public async Task<Result<BookingKtmbCheck?>> Record(
    Guid checkId,
    BookingKtmbCheckResult result,
    DateTime checkedAt
  )
  {
    try
    {
      var data = await db.BookingKtmbChecks.Where(x => x.Id == checkId).FirstOrDefaultAsync();
      if (data == null)
        return (BookingKtmbCheck?)null;
      data.Status = (byte)result.Status;
      data.CheckedAt = checkedAt;
      data.KtmbBookingNo = result.KtmbBookingNo;
      data.KtmbTicketNo = result.KtmbTicketNo;
      data.ClaimedByBookingId = result.ClaimedByBookingId;
      data.Error = result.Error is { Length: > ErrorMax } ? result.Error[..ErrorMax] : result.Error;
      await db.SaveChangesAsync();
      return data.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to record KTMB check '{CheckId}'", checkId);
      return e;
    }
  }
}

// pushes check requests onto the stream-cache list tin's recoverer pops (same
// envelope and cache as the terminator queue)
public class BookingKtmbCheckQueue(
  IOptions<KtmbCheckOption> options,
  IRedisClientFactory factory,
  ILogger<BookingKtmbCheckQueue> logger
) : IBookingKtmbCheckQueue
{
  private IRedisDatabase Redis => factory.GetRedisClient(Caches.Stream).Db0;

  public Task<Result<Unit>> Enqueue(BookingKtmbCheckRequest request)
  {
    try
    {
      new OtelRedisDatabase(this.Redis).QueuePush(options.Value.QueueName, request);
      return Task.FromResult((Result<Unit>)new Unit());
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to queue KTMB check {CheckId}", request.CheckId);
      return Task.FromResult((Result<Unit>)e);
    }
  }
}
