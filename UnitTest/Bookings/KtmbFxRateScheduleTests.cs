using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// The effective-dating rule behind the MYR -> SGD rate queue: the newest row
// whose EffectiveAt has passed wins; future rows queue; no configured row =
// NO rate (null — the analysis falls back to the estimate rather than
// guessing a conversion). The analysis SQL implements the same rule DB-side
// — this pure schedule is the in-memory source of truth.
public class KtmbFxRateScheduleTests
{
  private static readonly DateTime Now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

  private static KtmbFxRateChange Change(
    decimal rate,
    DateTime effectiveAt,
    DateTime? createdAt = null
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      Rate = rate,
      EffectiveAt = effectiveAt,
      CreatedAt = createdAt ?? effectiveAt,
    };

  [Fact]
  public void No_configured_rows_means_no_rate()
  {
    KtmbFxRateSchedule.EffectiveRate([], Now).Should().BeNull();

    var view = KtmbFxRateSchedule.View([], Now);
    view.Current.Should().BeNull();
    view.Recent.Should().BeEmpty();
  }

  [Fact]
  public void Newest_effective_row_wins()
  {
    var changes = new[]
    {
      Change(0.30m, Now.AddDays(-10)),
      Change(0.32m, Now.AddDays(-1)),
    };

    KtmbFxRateSchedule.EffectiveRate(changes, Now).Should().Be(0.32m);
  }

  [Fact]
  public void Future_rows_do_not_apply_yet_but_still_list_in_recent()
  {
    var future = Change(0.35m, Now.AddDays(2));
    var changes = new[] { Change(0.30m, Now.AddDays(-1)), future };

    var view = KtmbFxRateSchedule.View(changes, Now);

    view.Current!.Rate.Should().Be(0.30m);
    view.Recent.Should().Contain(x => x.Id == future.Id);
  }

  [Fact]
  public void A_queued_row_takes_over_once_its_instant_passes()
  {
    var changes = new[]
    {
      Change(0.30m, Now.AddDays(-10)),
      Change(0.35m, Now.AddDays(2)),
    };

    // the exact effective instant is inclusive (EffectiveAt <= at)
    KtmbFxRateSchedule.EffectiveRate(changes, Now.AddDays(2)).Should().Be(0.35m);
  }

  [Fact]
  public void Bookings_are_rated_at_their_own_completion_instant()
  {
    // the analysis converts each booking at ITS CompletedAt, not at "now":
    // a booking completed before a rate change keeps the old rate
    var changes = new[]
    {
      Change(0.30m, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
      Change(0.32m, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
    };

    var june = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
    var july = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
    KtmbFxRateSchedule.EffectiveRate(changes, june).Should().Be(0.30m);
    KtmbFxRateSchedule.EffectiveRate(changes, july).Should().Be(0.32m);
  }

  [Fact]
  public void A_booking_completed_before_the_first_effective_row_has_no_rate()
  {
    var changes = new[] { Change(0.30m, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)) };

    var june = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
    KtmbFxRateSchedule.EffectiveRate(changes, june).Should().BeNull();
  }

  [Fact]
  public void Same_effective_instant_ties_break_on_newest_created()
  {
    var effective = Now.AddDays(-1);
    var changes = new[]
    {
      Change(0.30m, effective, Now.AddDays(-3)),
      Change(0.31m, effective, Now.AddDays(-2)),
    };

    KtmbFxRateSchedule.EffectiveRate(changes, Now).Should().Be(0.31m);
  }

  [Fact]
  public void Recent_lists_every_row_most_recent_first()
  {
    var changes = new[]
    {
      Change(0.30m, Now.AddDays(-10)),
      Change(0.32m, Now.AddDays(-1)),
      Change(0.35m, Now.AddDays(2)),
    };

    var view = KtmbFxRateSchedule.View(changes, Now);

    view.Recent.Select(x => x.Rate).Should().Equal(0.35m, 0.32m, 0.30m);
    view.Current!.Rate.Should().Be(0.32m);
  }
}
