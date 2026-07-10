namespace Domain.User;

// PRICING roles for a purchase made on behalf of a target user: the TARGET
// user's roles ∪ their admin-granted ExtraRoles — the same union the Cost
// summary endpoints compute for that user, so the price previewed is the
// price charged (an extra-role-targeted discount must not vanish at
// purchase, and an admin-assisted purchase must not be priced with the
// admin's own roles). When the caller IS the target their JWT is
// authoritative, exactly like Cost/summary; when an admin purchases for
// someone else that user's JWT is absent, so fall back to the persisted
// Roles mirror (UserRecord.Roles, synced from the Descope token). Pricing
// only — never used for authorization.
public static class PurchasePricingRoles
{
  public static string[] For(
    bool callerIsTarget,
    IEnumerable<string> callerTokenRoles,
    UserRecord? target
  ) =>
    (callerIsTarget ? callerTokenRoles : target?.Roles ?? [])
      .Union(target?.ExtraRoles ?? [])
      .ToArray();
}
