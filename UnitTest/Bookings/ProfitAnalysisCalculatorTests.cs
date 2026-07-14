using Domain.Booking;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Bookings;

// The pure profit math behind GET Booking/analysis/profit: departure hours
// floor to quarter-day (6-hour) buckets, BOTH directions combine into one
// block, only Completed bookings count, the travel-date range is inclusive
// on both ends (null = unbounded), blocks without tickets never appear, and
// rows order by date then bucket. Revenue/Cost/WithActualCost sum over the
// block's slots — cost conversion itself happens upstream (SQL), the
// calculator only aggregates.
public class ProfitAnalysisCalculatorTests
{
  private static ProfitSlotSum Slot(
    TimeOnly time,
    DateOnly? date = null,
    TrainDirection direction = TrainDirection.JToW,
    BookStatus status = BookStatus.Completed,
    int tickets = 1,
    decimal revenue = 0m,
    decimal cost = 0m,
    int withActualCost = 0
  ) =>
    new()
    {
      Date = date ?? new DateOnly(2026, 8, 1),
      Direction = direction,
      Time = time,
      Status = status,
      Tickets = tickets,
      Revenue = revenue,
      Cost = cost,
      WithActualCost = withActualCost,
    };

  [Fact]
  public void Timeslots_in_the_same_bucket_collapse_and_sum_all_measures()
  {
    var rows = ProfitAnalysisCalculator.Analyze(
      [
        Slot(new TimeOnly(8, 30), tickets: 2, revenue: 90m, cost: 40m, withActualCost: 1),
        Slot(new TimeOnly(11, 0), tickets: 3, revenue: 135m, cost: 60.5m, withActualCost: 3),
      ],
      null,
      null
    );

    rows.Should().HaveCount(1);
    rows[0].QuarterStartHour.Should().Be(6);
    rows[0].Tickets.Should().Be(5);
    rows[0].Revenue.Should().Be(225m);
    rows[0].Cost.Should().Be(100.5m);
    rows[0].WithActualCost.Should().Be(4);
  }

  [Fact]
  public void Both_directions_combine_into_one_block()
  {
    var rows = ProfitAnalysisCalculator.Analyze(
      [
        Slot(
          new TimeOnly(8, 30),
          direction: TrainDirection.JToW,
          tickets: 2,
          revenue: 90m,
          cost: 40m,
          withActualCost: 2
        ),
        Slot(
          new TimeOnly(9, 45),
          direction: TrainDirection.WToJ,
          tickets: 1,
          revenue: 45m,
          cost: 20m,
          withActualCost: 1
        ),
      ],
      null,
      null
    );

    rows.Should().HaveCount(1);
    rows[0].Tickets.Should().Be(3);
    rows[0].Revenue.Should().Be(135m);
    rows[0].Cost.Should().Be(60m);
    rows[0].WithActualCost.Should().Be(3);
  }

  [Fact]
  public void Only_completed_bookings_count()
  {
    var rows = ProfitAnalysisCalculator.Analyze(
      [
        Slot(new TimeOnly(8, 30), status: BookStatus.Pending, revenue: 45m),
        Slot(new TimeOnly(8, 30), status: BookStatus.Buying, revenue: 45m),
        Slot(new TimeOnly(8, 30), status: BookStatus.Cancelled, revenue: 45m),
        Slot(new TimeOnly(8, 30), status: BookStatus.Refunded, revenue: 45m),
        Slot(new TimeOnly(8, 30), status: BookStatus.Terminated, revenue: 45m),
        Slot(new TimeOnly(8, 30), tickets: 4, revenue: 180m),
      ],
      null,
      null
    );

    rows.Should().HaveCount(1);
    rows[0].Tickets.Should().Be(4);
    rows[0].Revenue.Should().Be(180m);
  }

  [Fact]
  public void Travel_date_range_is_inclusive_on_both_ends()
  {
    var rows = ProfitAnalysisCalculator.Analyze(
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

    ProfitAnalysisCalculator.Analyze(slots, null, new DateOnly(2026, 8, 1))
      .Select(r => r.Date)
      .Should()
      .Equal(new DateOnly(2026, 8, 1));
    ProfitAnalysisCalculator.Analyze(slots, new DateOnly(2026, 8, 9), null)
      .Select(r => r.Date)
      .Should()
      .Equal(new DateOnly(2026, 8, 9));
    ProfitAnalysisCalculator.Analyze(slots, null, null).Should().HaveCount(2);
  }

  [Fact]
  public void Rows_order_by_date_then_bucket()
  {
    var rows = ProfitAnalysisCalculator.Analyze(
      [
        Slot(new TimeOnly(19, 0), new DateOnly(2026, 8, 2)),
        Slot(new TimeOnly(7, 0), new DateOnly(2026, 8, 2), TrainDirection.WToJ),
        Slot(new TimeOnly(13, 0), new DateOnly(2026, 8, 2)),
        Slot(new TimeOnly(8, 30), new DateOnly(2026, 8, 1), TrainDirection.WToJ),
      ],
      null,
      null
    );

    rows.Select(r => (r.Date.Day, r.QuarterStartHour))
      .Should()
      .Equal((1, 6), (2, 6), (2, 12), (2, 18));
  }

  [Fact]
  public void Empty_blocks_never_appear()
  {
    var rows = ProfitAnalysisCalculator.Analyze(
      [
        Slot(new TimeOnly(8, 30), tickets: 0),
        Slot(new TimeOnly(13, 0), tickets: 2, revenue: 90m),
      ],
      null,
      null
    );

    rows.Should().HaveCount(1);
    rows[0].QuarterStartHour.Should().Be(12);
  }

  [Fact]
  public void Bookings_without_actual_cost_contribute_zero_cost_but_full_revenue()
  {
    // upstream (SQL) costs a booking without an actual KTMB amount at 0 and
    // excludes it from WithActualCost — the block still carries its revenue
    var rows = ProfitAnalysisCalculator.Analyze(
      [
        Slot(new TimeOnly(8, 30), tickets: 1, revenue: 45m, cost: 20m, withActualCost: 1),
        Slot(new TimeOnly(9, 0), tickets: 1, revenue: 45m, cost: 0m, withActualCost: 0),
      ],
      null,
      null
    );

    rows.Should().HaveCount(1);
    rows[0].Revenue.Should().Be(90m);
    rows[0].Cost.Should().Be(20m);
    rows[0].WithActualCost.Should().Be(1);
    rows[0].Tickets.Should().Be(2);
  }

  [Fact]
  public void No_slots_produce_no_rows()
  {
    ProfitAnalysisCalculator.Analyze([], null, null).Should().BeEmpty();
  }
}
