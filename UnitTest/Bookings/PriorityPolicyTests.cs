using Domain.Booking;
using Domain.Discount;
using FluentAssertions;

namespace UnitTest.Bookings;

// The unified chain (PriorityRules.Match/Fee): first matching rule wins,
// target / clock-window / hours conditions, flat vs percent fees, and the
// null-hours (no booking in scope) semantics
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
    TimeOnly? winStart = null,
    TimeOnly? winEnd = null,
    PriorityFeeKind kind = PriorityFeeKind.Flat,
    decimal fee = 10m,
    int? slotCap = null,
    string name = "rule"
  ) =>
    new()
    {
      Name = name,
      Allow = allow,
      Target = target,
      MinHoursToDeparture = minHours,
      MaxHoursToDeparture = maxHours,
      WindowStartSgt = winStart,
      WindowEndSgt = winEnd,
      FeeKind = kind,
      FeeValue = fee,
      SlotCap = slotCap,
    };

  [Fact]
  public void Empty_chain_matches_nothing()
  {
    PriorityRules.Match([], Noon, 24m, "u1", ["vip"]).Should().BeNull();
  }

  [Fact]
  public void First_matching_rule_wins()
  {
    var chain = new[]
    {
      Rule(allow: true, target: Role("vip"), name: "first"),
      Rule(allow: false, target: Role("vip"), name: "second"),
    };

    PriorityRules.Match(chain, Noon, 24m, "u1", ["vip"])!.Name.Should().Be("first");
  }

  [Fact]
  public void Deny_inside_the_last_hours_blocks_a_broader_allow()
  {
    // "boosting closes once departure is under 6h away"
    var chain = new[] { Rule(allow: false, maxHours: 6m), Rule(allow: true) };

    PriorityRules.Match(chain, Noon, 2m, "u1", [])!.Allow.Should().BeFalse();
    PriorityRules.Match(chain, Noon, 7m, "u1", [])!.Allow.Should().BeTrue();
  }

  [Fact]
  public void Role_rules_mix_with_hours()
  {
    // VIPs anytime; everyone else only outside 48h
    var chain = new[]
    {
      Rule(allow: true, target: Role("vip"), fee: 0m),
      Rule(allow: true, minHours: 48m),
    };

    PriorityRules.Match(chain, Noon, 2m, "u1", ["vip"]).Should().NotBeNull();
    PriorityRules.Match(chain, Noon, 2m, "u1", []).Should().BeNull();
    PriorityRules.Match(chain, Noon, 72m, "u1", []).Should().NotBeNull();
  }

  [Fact]
  public void Hour_bounds_are_half_open()
  {
    var chain = new[] { Rule(allow: true, minHours: 24m, maxHours: 48m) };

    PriorityRules.Match(chain, Noon, 24m, "u1", []).Should().NotBeNull("min inclusive");
    PriorityRules.Match(chain, Noon, 48m, "u1", []).Should().BeNull("max exclusive");
  }

  [Fact]
  public void Clock_window_gates_the_rule_it_is_on()
  {
    var chain = new[]
    {
      Rule(allow: true, winStart: new TimeOnly(14, 0), winEnd: new TimeOnly(16, 0)),
      Rule(allow: false),
    };

    PriorityRules.Match(chain, Noon, 24m, "u1", [])!.Allow.Should().BeFalse("window closed");
    PriorityRules
      .Match(chain, new TimeOnly(15, 0), 24m, "u1", [])!
      .Allow.Should()
      .BeTrue("window open");
  }

  [Fact]
  public void Null_hours_skips_hour_bounded_rules_but_applies_unbounded_ones()
  {
    var chain = new[] { Rule(allow: false, maxHours: 6m), Rule(allow: true) };

    // without a booking in scope the 6h deny cannot be evaluated
    PriorityRules.Match(chain, Noon, null, "u1", [])!.Allow.Should().BeTrue();
  }

  [Fact]
  public void Flat_fee_is_the_value_itself()
  {
    PriorityRules.Fee(Rule(allow: true, fee: 12.5m), null).Should().Be(12.5m);
    PriorityRules.Fee(Rule(allow: true, fee: 12.5m), 100m).Should().Be(12.5m);
  }

  [Fact]
  public void Percent_fee_is_a_share_of_the_ticket_rounded_to_cents()
  {
    var rule = Rule(allow: true, kind: PriorityFeeKind.Percent, fee: 12.5m);

    PriorityRules.Fee(rule, 45m).Should().Be(5.63m, "12.5% of 45 = 5.625 -> 5.63");
    PriorityRules.Fee(rule, null).Should().BeNull("unknowable without a booking in scope");
  }

  [Fact]
  public void Zero_fee_means_free()
  {
    PriorityRules.Fee(Rule(allow: true, fee: 0m), null).Should().Be(0m);
    PriorityRules
      .Fee(Rule(allow: true, kind: PriorityFeeKind.Percent, fee: 0m), 45m)
      .Should()
      .Be(0m);
  }
}
