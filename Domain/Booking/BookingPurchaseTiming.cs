using CSharp_Result;
using Domain.Exceptions;

namespace Domain.Booking;

// One clock rule for every booking boundary: Date + Time are Singapore wall
// clock, and a customer may purchase only while at least three hours remain.
// Callers supply `now` so the conversion and cutoff stay pure and testable.
public static class BookingPurchaseTiming
{
  private static readonly TimeSpan SingaporeUtcOffset = TimeSpan.FromHours(8);

  public static readonly TimeSpan MinimumLeadTime = TimeSpan.FromHours(3);

  public static DateTime DepartureUtc(DateOnly date, TimeOnly time)
  {
    var singaporeWallClock = date.ToDateTime(time, DateTimeKind.Unspecified);
    return new DateTimeOffset(singaporeWallClock, SingaporeUtcOffset).UtcDateTime;
  }

  public static TimeSpan LeadTime(DateOnly date, TimeOnly time, DateTimeOffset now) =>
    DepartureUtc(date, time) - now.UtcDateTime;

  // "Three hours before departure" is inclusive: exactly 03:00:00 remains
  // purchasable; 02:59:59.999... does not.
  public static bool CanPurchase(DateOnly date, TimeOnly time, DateTimeOffset now) =>
    LeadTime(date, time, now) >= MinimumLeadTime;

  public static Result<BookingRecord> Validate(BookingRecord record, DateTimeOffset now)
  {
    if (CanPurchase(record.Date, record.Time, now))
      return record;

    return new InvalidBookingOperationException(
      "Bookings must be purchased at least 3 hours before departure",
      BookStatus.Pending,
      BookingOperations.Purchase
    );
  }
}
