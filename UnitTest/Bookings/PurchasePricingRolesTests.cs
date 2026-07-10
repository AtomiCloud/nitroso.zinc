using Domain.User;
using FluentAssertions;

namespace UnitTest.Bookings;

// The invariant: purchase-time pricing roles must equal the roles the Cost
// summary endpoint would price the TARGET user with — self-purchase uses the
// caller's JWT roles ∪ ExtraRoles (identical to Cost/summary for that
// caller), and an admin purchasing for X uses X's persisted Descope-synced
// Roles ∪ X's ExtraRoles, never the admin's own roles. No spurious 409, no
// admin-priced charge.
public class PurchasePricingRolesTests
{
  private static UserRecord Target(string[]? roles, string[] extraRoles) =>
    new()
    {
      Username = "target",
      Roles = roles,
      ExtraRoles = extraRoles,
    };

  [Fact]
  public void Self_purchase_uses_the_callers_jwt_roles_union_extra_roles()
  {
    // identical to Cost/summary: JWT roles ∪ ExtraRoles
    var roles = PurchasePricingRoles.For(true, ["member"], Target(["stale"], ["vip"]));

    roles.Should().BeEquivalentTo("member", "vip");
  }

  [Fact]
  public void Admin_assisted_purchase_uses_the_targets_persisted_roles_not_the_admins()
  {
    var roles = PurchasePricingRoles.For(false, ["admin"], Target(["member"], ["vip"]));

    roles.Should().BeEquivalentTo("member", "vip");
    roles.Should().NotContain("admin", "the admin's own roles must never price the booking");
  }

  [Fact]
  public void Admin_assisted_purchase_for_a_user_with_no_persisted_roles_prices_on_extra_roles_only()
  {
    var roles = PurchasePricingRoles.For(false, ["admin"], Target(null, ["vip"]));

    roles.Should().BeEquivalentTo("vip");
  }

  [Fact]
  public void Missing_target_user_yields_no_pricing_roles_for_admin_assisted_purchase()
  {
    var roles = PurchasePricingRoles.For(false, ["admin"], null);

    roles.Should().BeEmpty();
  }

  [Fact]
  public void Self_purchase_with_missing_user_row_still_uses_the_jwt_roles()
  {
    // the caller's own JWT stays authoritative even before the user row syncs
    var roles = PurchasePricingRoles.For(true, ["member"], null);

    roles.Should().BeEquivalentTo("member");
  }

  [Fact]
  public void The_union_deduplicates_roles_present_on_both_sides()
  {
    var roles = PurchasePricingRoles.For(true, ["vip"], Target(null, ["vip"]));

    roles.Should().HaveCount(1).And.Contain("vip");
  }
}
