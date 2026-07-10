using Domain.Cost;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Costs;

// The policy applicability matrix: every Match* dimension, their
// combinations, the lead-time boundary, the effective window and the
// enabled flag. Booking Date+Time are SGT wall clock; departure instant is
// date+time minus 8h (the BookingStats convention).
public class CostPolicyMatcherTests
{
  // 2026-07-15 is a Wednesday
  private static readonly BookingCostSpec Spec = new()
  {
    Date = new DateOnly(2026, 7, 15),
    Time = new TimeOnly(8, 30),
    Direction = TrainDirection.JToW,
  };

  // departure UTC = 2026-07-15 00:30 UTC; a week earlier leaves lots of lead
  private static readonly DateTime NowUtc = new(2026, 7, 8, 0, 30, 0, DateTimeKind.Utc);

  private static CostPolicyRecord Policy(
    bool enabled = true,
    DateOnly? matchDate = null,
    TimeOnly? matchTime = null,
    DayOfWeek? matchDayOfWeek = null,
    TrainDirection? matchDirection = null,
    int? leadTimeUnderHours = null,
    DateTime? effectiveAt = null,
    DateTime? expiresAt = null
  ) =>
    new()
    {
      Name = "policy",
      Enabled = enabled,
      MatchDate = matchDate,
      MatchTime = matchTime,
      MatchDayOfWeek = matchDayOfWeek,
      MatchDirection = matchDirection,
      LeadTimeUnderHours = leadTimeUnderHours,
      Amount = 1m,
      IsPercentage = false,
      EffectiveAt = effectiveAt,
      ExpiresAt = expiresAt,
    };

  [Fact]
  public void All_null_matches_everything()
  {
    CostPolicyMatcher.Applies(Policy(), Spec, NowUtc).Should().BeTrue();
  }

  [Fact]
  public void Disabled_policy_never_applies()
  {
    CostPolicyMatcher.Applies(Policy(enabled: false), Spec, NowUtc).Should().BeFalse();
  }

  [Fact]
  public void Date_dimension_matches_exactly()
  {
    CostPolicyMatcher.Applies(Policy(matchDate: Spec.Date), Spec, NowUtc).Should().BeTrue();
    CostPolicyMatcher
      .Applies(Policy(matchDate: Spec.Date.AddDays(1)), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Time_dimension_matches_exactly()
  {
    CostPolicyMatcher.Applies(Policy(matchTime: Spec.Time), Spec, NowUtc).Should().BeTrue();
    CostPolicyMatcher
      .Applies(Policy(matchTime: new TimeOnly(9, 45)), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void DayOfWeek_dimension_matches_the_travel_dates_weekday()
  {
    CostPolicyMatcher
      .Applies(Policy(matchDayOfWeek: DayOfWeek.Wednesday), Spec, NowUtc)
      .Should()
      .BeTrue();
    CostPolicyMatcher
      .Applies(Policy(matchDayOfWeek: DayOfWeek.Saturday), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Direction_dimension_matches_exactly()
  {
    CostPolicyMatcher
      .Applies(Policy(matchDirection: TrainDirection.JToW), Spec, NowUtc)
      .Should()
      .BeTrue();
    CostPolicyMatcher
      .Applies(Policy(matchDirection: TrainDirection.WToJ), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Combined_dimensions_all_must_match()
  {
    // both match
    CostPolicyMatcher
      .Applies(
        Policy(matchDayOfWeek: DayOfWeek.Wednesday, matchDirection: TrainDirection.JToW),
        Spec,
        NowUtc
      )
      .Should()
      .BeTrue();
    // one matches, one does not: AND semantics
    CostPolicyMatcher
      .Applies(
        Policy(matchDayOfWeek: DayOfWeek.Wednesday, matchDirection: TrainDirection.WToJ),
        Spec,
        NowUtc
      )
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Lead_time_boundary_is_strictly_under_the_threshold()
  {
    // departure UTC is 2026-07-15 00:30; now exactly 24h before
    var now = new DateTime(2026, 7, 14, 0, 30, 0, DateTimeKind.Utc);
    CostPolicyMatcher
      .Applies(Policy(leadTimeUnderHours: 24), Spec, now)
      .Should()
      .BeFalse("exactly 24h is not under 24h");

    CostPolicyMatcher
      .Applies(Policy(leadTimeUnderHours: 24), Spec, now.AddSeconds(1))
      .Should()
      .BeTrue("23:59:59 is under 24h");
    CostPolicyMatcher
      .Applies(Policy(leadTimeUnderHours: 24), Spec, now.AddSeconds(-1))
      .Should()
      .BeFalse("24:00:01 is over 24h");
  }

  [Fact]
  public void Lead_time_uses_sgt_departure_instant()
  {
    // 08:30 on 2026-07-15 is SGT wall clock = 00:30 UTC. At 23:30 UTC on the
    // 14th only 1h of lead remains, so a 2h cap applies — had the departure
    // been misread as 08:30 UTC the lead would be 9h and it would not.
    var now = new DateTime(2026, 7, 14, 23, 30, 0, DateTimeKind.Utc);
    CostPolicyMatcher.Applies(Policy(leadTimeUnderHours: 2), Spec, now).Should().BeTrue();
    CostPolicyMatcher.Applies(Policy(leadTimeUnderHours: 1), Spec, now).Should().BeFalse();
  }

  [Fact]
  public void Effective_window_is_half_open()
  {
    var effective = NowUtc.AddHours(-1);
    var expires = NowUtc.AddHours(1);

    // inside the window
    CostPolicyMatcher
      .Applies(Policy(effectiveAt: effective, expiresAt: expires), Spec, NowUtc)
      .Should()
      .BeTrue();

    // exactly at EffectiveAt: inclusive
    CostPolicyMatcher
      .Applies(Policy(effectiveAt: NowUtc, expiresAt: expires), Spec, NowUtc)
      .Should()
      .BeTrue();

    // exactly at ExpiresAt: exclusive
    CostPolicyMatcher
      .Applies(Policy(effectiveAt: effective, expiresAt: NowUtc), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Not_yet_effective_or_expired_policies_never_apply()
  {
    CostPolicyMatcher
      .Applies(Policy(effectiveAt: NowUtc.AddDays(1)), Spec, NowUtc)
      .Should()
      .BeFalse("the policy only starts tomorrow");
    CostPolicyMatcher
      .Applies(Policy(expiresAt: NowUtc.AddDays(-1)), Spec, NowUtc)
      .Should()
      .BeFalse("the policy expired yesterday");
  }

  [Fact]
  public void Unbounded_window_sides_are_open_ended()
  {
    CostPolicyMatcher
      .Applies(Policy(effectiveAt: null, expiresAt: NowUtc.AddDays(1)), Spec, NowUtc)
      .Should()
      .BeTrue();
    CostPolicyMatcher
      .Applies(Policy(effectiveAt: NowUtc.AddDays(-1), expiresAt: null), Spec, NowUtc)
      .Should()
      .BeTrue();
  }
}
