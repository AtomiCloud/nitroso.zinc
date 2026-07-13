using Domain.Booking;
using Domain.Discount;
using FluentAssertions;

namespace UnitTest.Bookings;

// The policy chain (PriorityRules.Decide): first matching rule wins, target
// and hours-to-departure conditions, fee overrides, fallback to the legacy
// gate, and the null-hours (no booking in scope) semantics
public class PriorityPolicyTests
{
  private static readonly TimeOnly Noon = new(12, 0);

  private static DiscountTarget Role(string role) =>
    new()
    {
      MatchMode = DiscountMatchMode.Any,
      Matches = [new DiscountMatch { Type = DiscountMatchType.Role, Value = role }],
    };

  private static PriorityPolicyRecord Rule(
    bool allow,
    DiscountTarget? target = null,
    decimal? minHours = null,
    decimal? maxHours = null,
    decimal? feeOverride = null
  ) =>
    new()
    {
      Name = "rule",
      Allow = allow,
      Target = target,
      MinHoursToDeparture = minHours,
      MaxHoursToDeparture = maxHours,
      FeeOverride = feeOverride,
    };

  private static PrioritySettingsRecord Settings(
    bool allowAll = false,
    IReadOnlyList<PriorityPolicyRecord>? policies = null,
    TimeOnly? start = null,
    TimeOnly? end = null
  ) =>
    new()
    {
      Fee = 10m,
      AllowAll = allowAll,
      WindowStartSgt = start,
      WindowEndSgt = end,
      Policies = policies ?? [],
    };

  [Fact]
  public void No_policies_falls_back_to_legacy_gate()
  {
    PriorityRules.Decide(false, Settings(), Noon, 24m, "u1", []).Eligible.Should().BeFalse();
    PriorityRules
      .Decide(false, Settings(allowAll: true), Noon, 24m, "u1", [])
      .Eligible.Should()
      .BeTrue();
    PriorityRules.Decide(true, Settings(), Noon, 24m, "u1", []).Eligible.Should().BeTrue();
  }

  [Fact]
  public void Deny_inside_min_hours_blocks_even_allow_all()
  {
    // "boosting closes once the departure is under 48h away":
    // allow when >= 48h, deny everyone else
    var s = Settings(
      allowAll: true,
      policies: [Rule(allow: true, minHours: 48m), Rule(allow: false)]
    );

    PriorityRules.Decide(false, s, Noon, 72m, "u1", []).Eligible.Should().BeTrue();
    PriorityRules.Decide(false, s, Noon, 12m, "u1", []).Eligible.Should().BeFalse();
  }

  [Fact]
  public void Role_rule_mixes_with_hours()
  {
    // VIPs boost anytime; everyone else only outside 6h
    var s = Settings(
      allowAll: true,
      policies: [Rule(allow: true, target: Role("vip")), Rule(allow: false, maxHours: 6m)]
    );

    PriorityRules.Decide(false, s, Noon, 2m, "u1", ["vip"]).Eligible.Should().BeTrue();
    PriorityRules.Decide(false, s, Noon, 2m, "u1", ["pleb"]).Eligible.Should().BeFalse();
    // outside 6h the deny rule no longer applies -> legacy gate (allowAll)
    PriorityRules.Decide(false, s, Noon, 7m, "u1", ["pleb"]).Eligible.Should().BeTrue();
  }

  [Fact]
  public void First_matching_rule_wins()
  {
    var s = Settings(
      allowAll: false,
      policies: [Rule(allow: true, target: Role("vip")), Rule(allow: false, target: Role("vip"))]
    );

    PriorityRules.Decide(false, s, Noon, 24m, "u1", ["vip"]).Eligible.Should().BeTrue();
  }

  [Fact]
  public void Allow_rule_fee_override_applies_and_deny_keeps_base_fee()
  {
    var s = Settings(
      allowAll: false,
      policies: [Rule(allow: true, minHours: 48m, feeOverride: 25m)]
    );

    var far = PriorityRules.Decide(false, s, Noon, 72m, "u1", []);
    far.Eligible.Should().BeTrue();
    far.Fee.Should().Be(25m);

    // no rule matches near departure -> legacy gate at the base fee
    var near = PriorityRules.Decide(false, s, Noon, 12m, "u1", []);
    near.Eligible.Should().BeFalse();
    near.Fee.Should().Be(10m);
  }

  [Fact]
  public void Hour_bounds_are_half_open()
  {
    var s = Settings(policies: [Rule(allow: true, minHours: 24m, maxHours: 48m)]);

    PriorityRules.Decide(false, s, Noon, 24m, "u1", []).Eligible.Should().BeTrue("min inclusive");
    PriorityRules
      .Decide(false, s, Noon, 48m, "u1", [])
      .Eligible.Should()
      .BeFalse("max exclusive");
  }

  [Fact]
  public void Null_hours_skips_hour_bounded_rules_but_applies_unbounded_ones()
  {
    var s = Settings(
      allowAll: true,
      policies: [Rule(allow: false, maxHours: 6m), Rule(allow: false, target: Role("banned"))]
    );

    // the hour-bounded deny cannot be evaluated without a booking; the
    // role-bound one can
    PriorityRules.Decide(false, s, Noon, null, "u1", []).Eligible.Should().BeTrue();
    PriorityRules.Decide(false, s, Noon, null, "u1", ["banned"]).Eligible.Should().BeFalse();
  }

  [Fact]
  public void Closed_window_blocks_before_any_policy_runs()
  {
    var s = Settings(
      policies: [Rule(allow: true)],
      start: new TimeOnly(0, 0),
      end: new TimeOnly(1, 0)
    );

    PriorityRules.Decide(true, s, Noon, 24m, "u1", []).Eligible.Should().BeFalse();
  }
}
