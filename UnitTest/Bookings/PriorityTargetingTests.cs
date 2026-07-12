using Domain.Booking;
using Domain.Discount;
using FluentAssertions;

namespace UnitTest.Bookings;

// Priority boost targeting: FreeTarget decides who boosts free, AccessTarget
// (when configured) replaces the legacy allowlist/AllowAll gate. Null targets
// keep legacy behavior byte-for-byte.
public class PriorityTargetingTests
{
  private static readonly TimeOnly Noon = new(12, 0);

  private static DiscountTarget Target(DiscountMatchMode mode, params DiscountMatch[] matches) =>
    new() { MatchMode = mode, Matches = matches };

  private static DiscountMatch Role(string value) =>
    new() { Type = DiscountMatchType.Role, Value = value };

  private static DiscountMatch UserId(string value) =>
    new() { Type = DiscountMatchType.UserId, Value = value };

  private static PrioritySettingsRecord Settings(
    bool allowAll = false,
    DiscountTarget? free = null,
    DiscountTarget? access = null,
    TimeOnly? start = null,
    TimeOnly? end = null
  ) =>
    PrioritySettingsRecord.Default with
    {
      AllowAll = allowAll,
      FreeTarget = free,
      AccessTarget = access,
      WindowStartSgt = start,
      WindowEndSgt = end,
    };

  // ---- FreeTarget: who boosts free ----

  [Fact]
  public void Null_free_target_means_nobody_is_free()
  {
    PriorityRules.Free(Settings(), "u1", ["admin"]).Should().BeFalse();
  }

  [Fact]
  public void Free_by_role_match()
  {
    var s = Settings(free: Target(DiscountMatchMode.Any, Role("admin")));
    PriorityRules.Free(s, "u1", ["admin"]).Should().BeTrue("admin role boosts free");
    PriorityRules.Free(s, "u1", ["field"]).Should().BeFalse();
    PriorityRules.Free(s, "u1", []).Should().BeFalse();
  }

  [Fact]
  public void Free_by_user_id_match()
  {
    var s = Settings(free: Target(DiscountMatchMode.Any, UserId("vip-user"), Role("admin")));
    PriorityRules.Free(s, "vip-user", []).Should().BeTrue("admin-added specific user");
    PriorityRules.Free(s, "other", []).Should().BeFalse();
  }

  [Fact]
  public void Free_all_mode_requires_every_match()
  {
    var s = Settings(free: Target(DiscountMatchMode.All, UserId("u1"), Role("vip")));
    PriorityRules.Free(s, "u1", ["vip"]).Should().BeTrue();
    PriorityRules.Free(s, "u1", []).Should().BeFalse();
    PriorityRules.Free(s, "u2", ["vip"]).Should().BeFalse();
  }

  [Fact]
  public void Free_none_mode_makes_everyone_free()
  {
    var s = Settings(free: Target(DiscountMatchMode.None));
    PriorityRules.Free(s, "anyone", []).Should().BeTrue();
  }

  // ---- AccessTarget: who may prioritize at all ----

  [Fact]
  public void Null_access_target_keeps_legacy_allowlist_semantics()
  {
    PriorityRules.Eligible(true, Settings(), Noon, "u1", []).Should().BeTrue("allowlisted");
    PriorityRules.Eligible(false, Settings(), Noon, "u1", []).Should().BeFalse();
    PriorityRules
      .Eligible(false, Settings(allowAll: true), Noon, "u1", [])
      .Should()
      .BeTrue("allow-all");
  }

  [Fact]
  public void Access_target_takes_precedence_over_allowlist()
  {
    var s = Settings(access: Target(DiscountMatchMode.Any, Role("vip")));
    // allowlisted but not in the target: the target wins — refused
    PriorityRules.Eligible(true, s, Noon, "u1", []).Should().BeFalse();
    // not allowlisted but in the target: allowed
    PriorityRules.Eligible(false, s, Noon, "u1", ["vip"]).Should().BeTrue();
  }

  [Fact]
  public void Access_target_takes_precedence_over_allow_all()
  {
    var s = Settings(allowAll: true, access: Target(DiscountMatchMode.Any, Role("vip")));
    PriorityRules.Eligible(false, s, Noon, "u1", []).Should().BeFalse("AllowAll is overridden");
    PriorityRules.Eligible(false, s, Noon, "u1", ["vip"]).Should().BeTrue();
  }

  [Fact]
  public void Access_target_by_user_id()
  {
    var s = Settings(access: Target(DiscountMatchMode.Any, UserId("u1")));
    PriorityRules.Eligible(false, s, Noon, "u1", []).Should().BeTrue();
    PriorityRules.Eligible(false, s, Noon, "u2", []).Should().BeFalse();
  }

  [Fact]
  public void Access_target_still_respects_the_window()
  {
    var s = Settings(
      access: Target(DiscountMatchMode.None),
      start: new TimeOnly(14, 0),
      end: new TimeOnly(16, 0)
    );
    PriorityRules.Eligible(false, s, new TimeOnly(15, 0), "u1", []).Should().BeTrue();
    PriorityRules
      .Eligible(false, s, Noon, "u1", [])
      .Should()
      .BeFalse("outside the window even though the target matches");
  }
}
