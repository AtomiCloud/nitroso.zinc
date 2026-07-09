using App.Modules.Bookings.Data;
using FluentAssertions;

namespace UnitTest.Bookings;

// Lead-time buckets: how long before departure (SGT wall-clock) a booking
// was made (CreatedAt, UTC). Boundaries are inclusive on the tight side —
// exactly 6h before departure is "6h".
public class BookingStatsTests
{
  // departure 2026-07-10 08:00 SGT = 2026-07-10 00:00 UTC
  private static readonly DateOnly Date = new(2026, 7, 10);
  private static readonly TimeOnly Time = new(8, 0);
  private static readonly DateTime DepartureUtc = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

  [Theory]
  [InlineData(1, "6h")]
  [InlineData(6, "6h")]
  [InlineData(7, "12h")]
  [InlineData(12, "12h")]
  [InlineData(13, "24h")]
  [InlineData(24, "24h")]
  [InlineData(25, "2d")]
  [InlineData(48, "2d")]
  [InlineData(49, "3d")]
  [InlineData(72, "3d")]
  [InlineData(73, "4d")]
  [InlineData(96, "4d")]
  [InlineData(97, "1w")]
  [InlineData(24 * 7, "1w")]
  [InlineData(24 * 7 + 1, "2w")]
  [InlineData(24 * 14, "2w")]
  [InlineData(24 * 21, "3w")]
  [InlineData(24 * 28, "4w")]
  [InlineData(24 * 31, "1m")]
  [InlineData(24 * 61, "2m")]
  [InlineData(24 * 92, "3m")]
  [InlineData(24 * 183, "6m")]
  [InlineData(24 * 200, "6m+")]
  public void Buckets_by_hours_before_departure(int hoursBefore, string expected)
  {
    var createdAt = DepartureUtc.AddHours(-hoursBefore);
    BookingStats.LeadTimeBucket(Date, Time, createdAt).Should().Be(expected);
  }

  [Fact]
  public void Booking_made_after_departure_lands_in_the_tightest_bucket()
  {
    BookingStats.LeadTimeBucket(Date, Time, DepartureUtc.AddHours(2)).Should().Be("6h");
  }

  [Fact]
  public void Departure_is_interpreted_as_SGT_not_UTC()
  {
    // 7h before the SGT departure instant; if the code wrongly treated the
    // travel time as UTC the lead would be 15h and the bucket "24h"
    var createdAt = DepartureUtc.AddHours(-7);
    BookingStats.LeadTimeBucket(Date, Time, createdAt).Should().Be("12h");
  }
}
