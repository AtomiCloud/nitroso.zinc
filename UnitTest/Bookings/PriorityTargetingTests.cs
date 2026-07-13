using App.Modules.Bookings.Data;
using App.Modules.Discounts.Data;
using Domain.Booking;
using Domain.Discount;
using FluentAssertions;

namespace UnitTest.Bookings;

// Legacy settings rows (pre-unification: fee/allow-all/window/targets +
// allowlist) must synthesize into unified rules that behave byte-for-byte
// like the old gates did
public class PriorityTargetingTests
{
  private static readonly TimeOnly Noon = new(12, 0);

  private static DiscountTargetData RoleTargetData(string role) =>
    new()
    {
      MatchMode = "any",
      Matches = [new DiscountMatchData { Type = "role", Value = role }],
    };

  private static PriorityPolicyRecord? MatchFor(
    List<PriorityPolicyRecord> rules,
    string userId,
    string[] roles
  ) => PriorityRules.Match(rules, Noon, 24m, userId, roles);

  [Fact]
  public void No_row_and_empty_allowlist_denies_everyone()
  {
    var rules = PriorityDataMapper.SynthesizeLegacy(null, []);
    MatchFor(rules, "u1", []).Should().BeNull();
  }

  [Fact]
  public void No_row_with_allowlist_admits_exactly_those_users_at_the_default_fee()
  {
    var rules = PriorityDataMapper.SynthesizeLegacy(null, ["u1", "u2"]);

    var hit = MatchFor(rules, "u1", []);
    hit.Should().NotBeNull();
    hit!.Allow.Should().BeTrue();
    PriorityRules.Fee(hit, null).Should().Be(10m, "the legacy default fee");
    MatchFor(rules, "u9", []).Should().BeNull();
  }

  [Fact]
  public void Allow_all_row_admits_anyone_at_the_row_fee()
  {
    var data = new PrioritySettingsData { Fee = 15m, AllowAll = true };
    var rules = PriorityDataMapper.SynthesizeLegacy(data, []);

    var hit = MatchFor(rules, "anyone", []);
    hit.Should().NotBeNull();
    PriorityRules.Fee(hit!, null).Should().Be(15m);
  }

  [Fact]
  public void Free_target_synthesizes_a_zero_fee_rule_ahead_of_the_access_rule()
  {
    var data = new PrioritySettingsData
    {
      Fee = 10m,
      AllowAll = true,
      FreeTarget = RoleTargetData("vip"),
    };
    var rules = PriorityDataMapper.SynthesizeLegacy(data, []);

    var vip = MatchFor(rules, "u1", ["vip"]);
    PriorityRules.Fee(vip!, null).Should().Be(0m, "free target = free rule");
    var pleb = MatchFor(rules, "u1", []);
    PriorityRules.Fee(pleb!, null).Should().Be(10m);
  }

  [Fact]
  public void Access_target_row_replaces_the_allowlist()
  {
    var data = new PrioritySettingsData { Fee = 10m, AccessTarget = RoleTargetData("vip") };
    // u1 is allowlisted, but the access target took precedence in the legacy
    // system — synthesis must preserve that
    var rules = PriorityDataMapper.SynthesizeLegacy(data, ["u1"]);

    MatchFor(rules, "u1", []).Should().BeNull();
    MatchFor(rules, "u1", ["vip"]).Should().NotBeNull();
  }

  [Fact]
  public void Legacy_window_and_slot_cap_carry_onto_the_synthesized_rules()
  {
    var data = new PrioritySettingsData
    {
      Fee = 10m,
      AllowAll = true,
      WindowStartSgt = new TimeOnly(14, 0),
      WindowEndSgt = new TimeOnly(16, 0),
      SlotCap = 3,
    };
    var rules = PriorityDataMapper.SynthesizeLegacy(data, []);

    // closed at noon, open at 15:00 — and the cap rides along
    PriorityRules.Match(rules, Noon, 24m, "u1", []).Should().BeNull();
    var hit = PriorityRules.Match(rules, new TimeOnly(15, 0), 24m, "u1", []);
    hit.Should().NotBeNull();
    hit!.SlotCap.Should().Be(3);
  }

  [Fact]
  public void Unified_rows_never_synthesize()
  {
    // a row that carries policies (even '[]') is post-unification: the
    // repository returns them verbatim and never calls synthesis — this test
    // just pins the '[]' = deny-everyone reading at the rules level
    PriorityRules.Match([], Noon, 24m, "u1", ["vip"]).Should().BeNull();
  }
}
