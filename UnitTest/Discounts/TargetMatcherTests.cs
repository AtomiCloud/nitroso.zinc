using Domain.Discount;
using FluentAssertions;

namespace UnitTest.Discounts;

// The shared user/role targeting semantics (discounts + priority boost
// free/access targeting): All = every match must hit, Any = at least one,
// None = matches everyone.
public class TargetMatcherTests
{
  private static DiscountTarget Target(DiscountMatchMode mode, params DiscountMatch[] matches) =>
    new() { MatchMode = mode, Matches = matches };

  private static DiscountMatch Role(string value) =>
    new() { Type = DiscountMatchType.Role, Value = value };

  private static DiscountMatch UserId(string value) =>
    new() { Type = DiscountMatchType.UserId, Value = value };

  [Fact]
  public void None_matches_everyone_even_with_no_matches()
  {
    TargetMatcher.Matches(Target(DiscountMatchMode.None), "u1", []).Should().BeTrue();
    TargetMatcher
      .Matches(Target(DiscountMatchMode.None, Role("admin")), "u1", [])
      .Should()
      .BeTrue("None ignores the match list");
  }

  [Fact]
  public void Any_hits_on_role_membership()
  {
    var t = Target(DiscountMatchMode.Any, Role("admin"), Role("vip"));
    TargetMatcher.Matches(t, "u1", ["vip"]).Should().BeTrue();
    TargetMatcher.Matches(t, "u1", ["staff"]).Should().BeFalse();
    TargetMatcher.Matches(t, "u1", []).Should().BeFalse();
  }

  [Fact]
  public void Any_hits_on_user_id()
  {
    var t = Target(DiscountMatchMode.Any, UserId("u1"), Role("admin"));
    TargetMatcher.Matches(t, "u1", []).Should().BeTrue("targeted by id");
    TargetMatcher.Matches(t, "u2", []).Should().BeFalse();
    TargetMatcher.Matches(t, "u2", ["admin"]).Should().BeTrue("targeted by role");
  }

  [Fact]
  public void All_requires_every_match_to_hit()
  {
    var t = Target(DiscountMatchMode.All, UserId("u1"), Role("vip"));
    TargetMatcher.Matches(t, "u1", ["vip"]).Should().BeTrue();
    TargetMatcher.Matches(t, "u1", []).Should().BeFalse("role missing");
    TargetMatcher.Matches(t, "u2", ["vip"]).Should().BeFalse("wrong user");
  }

  [Fact]
  public void All_with_empty_matches_matches_everyone()
  {
    // vacuous truth, mirroring the discount matcher's historical behavior
    TargetMatcher.Matches(Target(DiscountMatchMode.All), "u1", []).Should().BeTrue();
  }

  [Fact]
  public void Any_with_empty_matches_matches_no_one()
  {
    TargetMatcher.Matches(Target(DiscountMatchMode.Any), "u1", ["admin"]).Should().BeFalse();
  }
}
