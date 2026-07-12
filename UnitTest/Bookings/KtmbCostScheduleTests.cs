using Domain.Booking;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Bookings;

// The effective-dating rule behind the KTMB ticket cost queue: per
// direction, the newest row whose EffectiveAt has passed wins; future rows
// queue; no configured row = cost 0. The analysis SQL implements the same
// rule DB-side — this pure schedule is the in-memory source of truth.
public class KtmbCostScheduleTests
{
  private static readonly DateTime Now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

  private static KtmbCostChange Change(
    TrainDirection direction,
    decimal cost,
    DateTime effectiveAt,
    DateTime? createdAt = null
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      Direction = direction,
      Cost = cost,
      EffectiveAt = effectiveAt,
      CreatedAt = createdAt ?? effectiveAt,
    };

  [Fact]
  public void No_configured_rows_cost_zero()
  {
    KtmbCostSchedule.EffectiveCost([], TrainDirection.JToW, Now).Should().Be(0m);

    var view = KtmbCostSchedule.View([], Now);
    view.Current[TrainDirection.JToW].Should().Be(0m);
    view.Current[TrainDirection.WToJ].Should().Be(0m);
    view.Upcoming.Should().BeEmpty();
  }

  [Fact]
  public void Newest_effective_row_wins()
  {
    var changes = new[]
    {
      Change(TrainDirection.JToW, 10m, Now.AddDays(-10)),
      Change(TrainDirection.JToW, 12m, Now.AddDays(-1)),
    };

    KtmbCostSchedule.EffectiveCost(changes, TrainDirection.JToW, Now).Should().Be(12m);
  }

  [Fact]
  public void Directions_are_independent()
  {
    var changes = new[]
    {
      Change(TrainDirection.JToW, 10m, Now.AddDays(-1)),
      Change(TrainDirection.WToJ, 7m, Now.AddDays(-1)),
    };

    KtmbCostSchedule.EffectiveCost(changes, TrainDirection.JToW, Now).Should().Be(10m);
    KtmbCostSchedule.EffectiveCost(changes, TrainDirection.WToJ, Now).Should().Be(7m);
  }

  [Fact]
  public void One_direction_configured_leaves_the_other_at_zero()
  {
    var changes = new[] { Change(TrainDirection.JToW, 10m, Now.AddDays(-1)) };

    var view = KtmbCostSchedule.View(changes, Now);

    view.Current[TrainDirection.JToW].Should().Be(10m);
    view.Current[TrainDirection.WToJ].Should().Be(0m);
  }

  [Fact]
  public void Future_rows_queue_and_do_not_apply_yet()
  {
    var future = Change(TrainDirection.JToW, 15m, Now.AddDays(2));
    var changes = new[] { Change(TrainDirection.JToW, 10m, Now.AddDays(-1)), future };

    var view = KtmbCostSchedule.View(changes, Now);

    view.Current[TrainDirection.JToW].Should().Be(10m);
    view.Upcoming.Should().ContainSingle(x => x.Id == future.Id);
  }

  [Fact]
  public void A_queued_row_takes_over_once_its_instant_passes()
  {
    var changes = new[]
    {
      Change(TrainDirection.JToW, 10m, Now.AddDays(-10)),
      Change(TrainDirection.JToW, 15m, Now.AddDays(2)),
    };

    // the exact effective instant is inclusive (EffectiveAt <= at)
    KtmbCostSchedule
      .EffectiveCost(changes, TrainDirection.JToW, Now.AddDays(2))
      .Should()
      .Be(15m);
  }

  [Fact]
  public void Bookings_are_costed_at_their_own_completion_instant()
  {
    // the analysis costs each booking at ITS CompletedAt, not at "now": a
    // booking completed before a price change keeps the old cost
    var changes = new[]
    {
      Change(TrainDirection.JToW, 10m, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
      Change(TrainDirection.JToW, 12m, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
    };

    var june = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
    var july = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
    KtmbCostSchedule.EffectiveCost(changes, TrainDirection.JToW, june).Should().Be(10m);
    KtmbCostSchedule.EffectiveCost(changes, TrainDirection.JToW, july).Should().Be(12m);
  }

  [Fact]
  public void Same_effective_instant_ties_break_on_newest_created()
  {
    var effective = Now.AddDays(-1);
    var changes = new[]
    {
      Change(TrainDirection.JToW, 10m, effective, Now.AddDays(-3)),
      Change(TrainDirection.JToW, 11m, effective, Now.AddDays(-2)),
    };

    KtmbCostSchedule.EffectiveCost(changes, TrainDirection.JToW, Now).Should().Be(11m);
  }

  [Fact]
  public void Upcoming_lists_soonest_first_across_directions()
  {
    var changes = new[]
    {
      Change(TrainDirection.WToJ, 9m, Now.AddDays(5)),
      Change(TrainDirection.JToW, 15m, Now.AddDays(2)),
    };

    var view = KtmbCostSchedule.View(changes, Now);

    view.Upcoming.Select(x => x.Cost).Should().Equal(15m, 9m);
  }
}
