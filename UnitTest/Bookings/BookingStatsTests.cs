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

  // The ladder is defined ONCE (BookingStats.LeadLadder) and BOTH the C#
  // helper and the SQL CASE in the booking_stats materialized view are
  // generated from it. This pins the single source to the documented
  // boundary table so neither side can drift from the contract.
  [Fact]
  public void Lead_ladder_matches_the_documented_boundary_table()
  {
    BookingStats
      .LeadLadder.Should()
      .Equal(
        (6, "6h"),
        (12, "12h"),
        (24, "24h"),
        (48, "2d"),
        (72, "3d"),
        (96, "4d"),
        (168, "1w"),
        (336, "2w"),
        (504, "3w"),
        (672, "4w"),
        (744, "1m"),
        (1464, "2m"),
        (2208, "3m"),
        (4392, "6m")
      );
    BookingStats.LeadOverflow.Should().Be("6m+");
  }

  [Fact]
  public void Bucket_order_is_the_ladder_labels_plus_overflow()
  {
    BookingStats
      .BucketOrder.Should()
      .Equal(
        "6h",
        "12h",
        "24h",
        "2d",
        "3d",
        "4d",
        "1w",
        "2w",
        "3w",
        "4w",
        "1m",
        "2m",
        "3m",
        "6m",
        "6m+"
      );
  }

  // the SQL twin: every rung becomes a `<= bound THEN 'label'` arm in order,
  // with the overflow as ELSE — same inclusive-upper-bound semantics as the
  // C# helper
  [Fact]
  public void CaseSql_mirrors_the_ladder_boundaries_and_labels()
  {
    var sql = BookingStats.CaseSql("x", BookingStats.LeadLadder, BookingStats.LeadOverflow);

    var arms = string.Join(
      " ",
      BookingStats.LeadLadder.Select(l => $"WHEN x <= {l.Hours:0.####} THEN '{l.Label}'")
    );
    sql.Should().Be($"CASE {arms} ELSE '{BookingStats.LeadOverflow}' END");
  }
}

// Demand buckets: bookings (any status/priority) sharing the same
// date+time+direction slot instance. Inclusive upper bounds.
public class BookingStatsDemandBucketTests
{
  [Theory]
  [InlineData(0, "0-5")]
  [InlineData(1, "0-5")]
  [InlineData(5, "0-5")]
  [InlineData(6, "5-10")]
  [InlineData(10, "5-10")]
  [InlineData(11, "10-20")]
  [InlineData(20, "10-20")]
  [InlineData(21, "20-30")]
  [InlineData(30, "20-30")]
  [InlineData(31, "30+")]
  [InlineData(1000, "30+")]
  public void Buckets_by_slot_count(int slotCount, string expected)
  {
    BookingStats.DemandBucket(slotCount).Should().Be(expected);
  }
}

// Delivery buckets: hours from CompletedAt to departure for completed
// bookings. Inclusive upper bounds; deliveries after departure (negative
// hours, should not happen) land in the tightest bucket.
public class BookingStatsDeliveryBucketTests
{
  [Theory]
  [InlineData(0.5, "1h")]
  [InlineData(1, "1h")]
  [InlineData(1.5, "2h")]
  [InlineData(2, "2h")]
  [InlineData(3, "3h")]
  [InlineData(4, "4h")]
  [InlineData(5, "5h")]
  [InlineData(6, "6h")]
  [InlineData(7, "12h")]
  [InlineData(12, "12h")]
  [InlineData(13, "24h")]
  [InlineData(24, "24h")]
  [InlineData(25, "48h")]
  [InlineData(48, "48h")]
  [InlineData(49, "48h+")]
  [InlineData(-2, "1h")]
  public void Buckets_by_hours_before_departure(double hours, string expected)
  {
    BookingStats.DeliveryBucket(hours).Should().Be(expected);
  }

  [Fact]
  public void Delivery_ladder_matches_the_documented_boundary_table()
  {
    BookingStats
      .DeliveryLadder.Should()
      .Equal(
        (1, "1h"),
        (2, "2h"),
        (3, "3h"),
        (4, "4h"),
        (5, "5h"),
        (6, "6h"),
        (12, "12h"),
        (24, "24h"),
        (48, "48h")
      );
    BookingStats.DeliveryOverflow.Should().Be("48h+");
  }

  [Fact]
  public void Demand_ladder_matches_the_documented_boundary_table()
  {
    BookingStats
      .DemandLadder.Should()
      .Equal((5, "0-5"), (10, "5-10"), (20, "10-20"), (30, "20-30"));
    BookingStats.DemandOverflow.Should().Be("30+");
  }
}
