using CSharp_Result;
using Microsoft.Extensions.Logging;

namespace Domain.Booking;

// An admin's "does OUR KTMB account hold a ticket for this booking's
// passenger/date/direction?" question. zinc cannot talk to KTMB: it records
// the request, queues it for tin's recoverer (which runs the same
// upcoming-ticket scan it uses to resolve Recovering bookings) and stores the
// answer tin writes back. Strictly READ-ONLY with respect to the booking: no
// status, money or ticket field is ever touched by a check.
public enum KtmbCheckStatus
{
  // queued, tin has not answered yet
  Pending,

  // a ticket for the passport at that departure+direction is on our account
  Found,

  // a definitive scan found no such ticket on our account
  NotFound,

  // departure has passed: the upcoming-ticket list can no longer verify it
  Departed,

  // the scan was inconclusive or failed (see Error)
  Error,
}

public record BookingKtmbCheck
{
  public required Guid Id { get; init; }

  public required Guid BookingId { get; init; }

  public required KtmbCheckStatus Status { get; init; }

  public required DateTime RequestedAt { get; init; }

  // the admin's sub
  public required string? RequestedBy { get; init; }

  public required DateTime? CheckedAt { get; init; }

  public required string? KtmbBookingNo { get; init; }

  public required string? KtmbTicketNo { get; init; }

  // the Completed zinc booking already recording this KTMB booking number
  // (may be the checked booking itself); null when unclaimed or not found
  public required Guid? ClaimedByBookingId { get; init; }

  public required string? Error { get; init; }
}

// what tin reports back for a check
public record BookingKtmbCheckResult
{
  public required KtmbCheckStatus Status { get; init; }

  public string? KtmbBookingNo { get; init; }

  public string? KtmbTicketNo { get; init; }

  public Guid? ClaimedByBookingId { get; init; }

  public string? Error { get; init; }
}

// the queue payload tin consumes: ids only, no passenger data — tin reads
// the booking from zinc itself
public record BookingKtmbCheckRequest
{
  public required Guid CheckId { get; init; }

  public required Guid BookingId { get; init; }
}

public interface IBookingKtmbCheckRepository
{
  Task<Result<BookingKtmbCheck>> Create(Guid bookingId, string? requestedBy, DateTime requestedAt);

  // the most recently requested check for the booking; null if never checked
  Task<Result<BookingKtmbCheck?>> Latest(Guid bookingId);

  Task<Result<BookingKtmbCheck?>> Get(Guid checkId);

  // writes the answer onto a check only while it is still Pending (atomic:
  // the first answer wins) and returns the check as stored; null if the check
  // does not exist
  Task<Result<BookingKtmbCheck?>> Record(
    Guid checkId,
    BookingKtmbCheckResult result,
    DateTime checkedAt
  );
}

public interface IBookingKtmbCheckQueue
{
  Task<Result<Unit>> Enqueue(BookingKtmbCheckRequest request);
}

public interface IBookingKtmbCheckService
{
  // queue a check (or return the one already in flight); null if the booking
  // does not exist
  Task<Result<BookingKtmbCheck?>> Request(Guid bookingId, string? requestedBy);

  Task<Result<BookingKtmbCheck?>> Latest(Guid bookingId);

  // tin's answer; the first answer wins (a check that already has one is
  // returned unchanged); null if the check does not exist
  Task<Result<BookingKtmbCheck?>> Record(Guid checkId, BookingKtmbCheckResult result);
}

public class BookingKtmbCheckService(
  IBookingRepository bookings,
  IBookingKtmbCheckRepository checks,
  IBookingKtmbCheckQueue queue,
  ILogger<BookingKtmbCheckService> logger,
  TimeProvider? timeProvider = null
) : IBookingKtmbCheckService
{
  // a Pending check younger than this is reused instead of queueing another
  // KTMB scan (double clicks, several admins on the same booking); matches
  // argon's 90s "no answer" timeout so the admin can ask again right after it
  public static readonly TimeSpan InFlightReuse = TimeSpan.FromSeconds(90);

  private TimeProvider Clock { get; } = timeProvider ?? TimeProvider.System;

  public async Task<Result<BookingKtmbCheck?>> Request(Guid bookingId, string? requestedBy)
  {
    var bookingR = await bookings.Get(null, bookingId);
    if (!bookingR.IsSuccess())
      return bookingR.FailureOrDefault();
    if (bookingR.SuccessOrDefault() == null)
      return (BookingKtmbCheck?)null;

    var now = this.Clock.GetUtcNow().UtcDateTime;
    var latestR = await checks.Latest(bookingId);
    if (!latestR.IsSuccess())
      return latestR.FailureOrDefault();
    var latest = latestR.SuccessOrDefault();
    if (
      latest is { Status: KtmbCheckStatus.Pending }
      && now - DateTime.SpecifyKind(latest.RequestedAt, DateTimeKind.Utc) < InFlightReuse
    )
      return latest;

    var createdR = await checks.Create(bookingId, requestedBy, now);
    if (!createdR.IsSuccess())
      return createdR.FailureOrDefault();
    var created = createdR.SuccessOrDefault();

    var queued = await queue.Enqueue(
      new BookingKtmbCheckRequest { CheckId = created.Id, BookingId = bookingId }
    );
    if (queued.IsSuccess())
      return created;

    // never leave a check Pending that tin will never see
    logger.LogError(
      queued.FailureOrDefault(),
      "Failed to queue KTMB check {CheckId} for Booking {BookingId}",
      created.Id,
      bookingId
    );
    return await checks.Record(
      created.Id,
      new BookingKtmbCheckResult
      {
        Status = KtmbCheckStatus.Error,
        Error = "Could not queue the check for tin; try again",
      },
      now
    );
  }

  public Task<Result<BookingKtmbCheck?>> Latest(Guid bookingId) => checks.Latest(bookingId);

  public async Task<Result<BookingKtmbCheck?>> Record(Guid checkId, BookingKtmbCheckResult result)
  {
    var existingR = await checks.Get(checkId);
    if (!existingR.IsSuccess())
      return existingR.FailureOrDefault();
    var existing = existingR.SuccessOrDefault();
    if (existing == null)
      return (BookingKtmbCheck?)null;
    // fast path; the repository write re-checks Pending atomically
    if (existing.Status != KtmbCheckStatus.Pending)
      return existing;

    return await checks.Record(checkId, result, this.Clock.GetUtcNow().UtcDateTime);
  }
}
