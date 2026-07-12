namespace Domain.Discount;

// Pure user/role targeting semantics for a DiscountTarget-shaped blob, shared
// by discount matching and priority-boost targeting so the two features can
// never drift: All = every match must hit, Any = at least one match must hit,
// None = matches everyone.
public static class TargetMatcher
{
  public static bool Matches(DiscountTarget target, string userId, string[] roles)
  {
    return target.MatchMode switch
    {
      DiscountMatchMode.All => target.Matches.All(x => Hit(x, userId, roles)),
      DiscountMatchMode.Any => target.Matches.Any(x => Hit(x, userId, roles)),
      DiscountMatchMode.None => true,
      _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
  }

  private static bool Hit(DiscountMatch match, string userId, string[] roles) =>
    match.Type switch
    {
      DiscountMatchType.UserId => match.Value == userId,
      DiscountMatchType.Role => roles.Contains(match.Value),
      _ => throw new ArgumentOutOfRangeException(nameof(match)),
    };
}
