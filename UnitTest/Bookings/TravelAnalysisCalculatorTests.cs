using Domain.Booking;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Bookings;

// The pure demand math behind GET Booking/analysis/travel: departure hours
// floor to quarter-day (6-hour) buckets, only Completed bookings count, the
// travel-date range is inclusive on both ends (null = unbounded), buckets
// without tickets never appear, and rows order by date, direction, bucket.
public class TravelAnalysisCalculatorTests
{
  private static TravelSlotCount Slot(
    TimeOnly time,
    DateOnly? date = null,
    TrainDirection direction = TrainDirection.JToW,
    BookStatus status = BookStatus.Completed,
    int tickets = 1
  ) =>
    new()
    {
      Date = date ?? new DateOnly(2026, 8, 1),
      Direction = direction,
      Time = time,
      Status = status,
      Tickets = tickets,
    };

  [Theory]
  [InlineData(0, 0, 0)]
  [InlineData(5, 59, 0)]
  [InlineData(6, 0, 6)]
  [InlineData(11, 59, 6)]
  [InlineData(12, 0, 12)]
  [InlineData(17, 59, 12)]
  [InlineData(18, 0, 18)]
  [InlineData(23, 59, 18)]
  public void Departure_hours_floor_to_quarter_day_buckets(int hour, int minute, int expected)
  {
    TravelAnalysisCalculator.QuarterStartHour(new TimeOnly(hour, minute)).Should().Be(expected);
  }

  [Fact]
  public void Timeslots_in_the_same_bucket_collapse_and_sum_tickets()
  {
    var rows = TravelAnalysisCalculator.Analyze(
      [Slot(new TimeOnly(8, 30), tickets: 2), Slot(new TimeOnly(11, 0), tickets: 3)],
      null,
      null
    );

    rows.Should().HaveCount(1);
    rows[0].QuarterStartHour.Should().Be(6);
    rows[0].Tickets.Should().Be(5);
  }

  [Fact]
  public void Only_completed_bookings_count()
  {
    var rows = TravelAnalysisCalculator.Analyze(
      [
        Slot(new TimeOnly(8, 30), status: BookStatus.Pending),
        Slot(new TimeOnly(8, 30), status: BookStatus.Buying),
        Slot(new TimeOnly(8, 30), status: BookStatus.Cancelled),
        Slot(new TimeOnly(8, 30), status: BookStatus.Refunded),
        Slot(new TimeOnly(8, 30), status: BookStatus.Terminated),
        Slot(new TimeOnly(8, 30), tickets: 4),
      ],
      null,
      null
    );

    rows.Should().HaveCount(1);
    rows[0].Tickets.Should().Be(4);
  }

  [Fact]
  public void Travel_date_range_is_inclusive_on_both_ends()
  {
    var rows = TravelAnalysisCalculator.Analyze(
      [
        Slot(new TimeOnly(8, 30), new DateOnly(2026, 8, 1)),
        Slot(new TimeOnly(8, 30), new DateOnly(2026, 8, 2)),
        Slot(new TimeOnly(8, 30), new DateOnly(2026, 8, 3)),
        Slot(new TimeOnly(8, 30), new DateOnly(2026, 8, 4)),
      ],
      new DateOnly(2026, 8, 2),
      new DateOnly(2026, 8, 3)
    );

    rows.Select(r => r.Date)
      .Should()
      .Equal(new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 3));
  }

  [Fact]
  public void Null_bounds_are_unbounded()
  {
    var slots = new[]
    {
      Slot(new TimeOnly(8, 30), new DateOnly(2026, 8, 1)),
      Slot(new TimeOnly(8, 30), new DateOnly(2026, 8, 9)),
    };

    TravelAnalysisCalculator.Analyze(slots, null, new DateOnly(2026, 8, 1))
      .Select(r => r.Date)
      .Should()
      .Equal(new DateOnly(2026, 8, 1));
    TravelAnalysisCalculator.Analyze(slots, new DateOnly(2026, 8, 9), null)
      .Select(r => r.Date)
      .Should()
      .Equal(new DateOnly(2026, 8, 9));
    TravelAnalysisCalculator.Analyze(slots, null, null).Should().HaveCount(2);
  }

  [Fact]
  public void Rows_order_by_date_then_direction_then_bucket()
  {
    var rows = TravelAnalysisCalculator.Analyze(
      [
        Slot(new TimeOnly(19, 0), new DateOnly(2026, 8, 2), TrainDirection.WToJ),
        Slot(new TimeOnly(7, 0), new DateOnly(2026, 8, 2), TrainDirection.WToJ),
        Slot(new TimeOnly(13, 0), new DateOnly(2026, 8, 2), TrainDirection.JToW),
        Slot(new TimeOnly(8, 30), new DateOnly(2026, 8, 1), TrainDirection.WToJ),
      ],
      null,
      null
    );

    rows.Select(r => (r.Date.Day, r.Direction, r.QuarterStartHour))
      .Should()
      .Equal(
        (1, TrainDirection.WToJ, 6),
        (2, TrainDirection.JToW, 12),
        (2, TrainDirection.WToJ, 6),
        (2, TrainDirection.WToJ, 18)
      );
  }

  [Fact]
  public void Empty_buckets_never_appear()
  {
    var rows = TravelAnalysisCalculator.Analyze(
      [Slot(new TimeOnly(8, 30), tickets: 0), Slot(new TimeOnly(13, 0), tickets: 2)],
      null,
      null
    );

    rows.Should().HaveCount(1);
    rows[0].QuarterStartHour.Should().Be(12);
  }

  [Fact]
  public void No_slots_produce_no_rows()
  {
    TravelAnalysisCalculator.Analyze([], null, null).Should().BeEmpty();
  }
}
