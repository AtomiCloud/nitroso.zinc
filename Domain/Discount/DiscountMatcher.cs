using Microsoft.Extensions.Logging;

namespace Domain.Discount;

public interface IDiscountMatcher
{
  bool Match(DiscountTarget discount, string userId, string[] roles);
}

public class DiscountMatcher(ILogger<DiscountMatcher> logger) : IDiscountMatcher
{
  public bool Match(DiscountTarget discount, string userId, string[] roles)
  {
    logger.LogInformation(
      "Matching: {Type} {@Values} for {@UserId} with {@Roles}",
      discount.MatchMode,
      discount.Matches.ToArray(),
      userId,
      roles
    );
    return discount.MatchMode switch
    {
      DiscountMatchMode.All => this.MatchAll(discount.Matches, userId, roles),
      DiscountMatchMode.Any => this.MatchAny(discount.Matches, userId, roles),
      DiscountMatchMode.None => true,
      _ => throw new ArgumentOutOfRangeException(),
    };
  }

  private bool MatchAll(IEnumerable<DiscountMatch> matches, string userId, string[] roles)
  {
    return matches.All(x =>
      x.Type switch
      {
        DiscountMatchType.UserId => x.Value == userId,
        DiscountMatchType.Role => roles.Contains(x.Value),
        _ => throw new ArgumentOutOfRangeException(),
      }
    );
  }

  private bool MatchAny(IEnumerable<DiscountMatch> matches, string userId, string[] roles)
  {
    return matches.Any(x =>
      x.Type switch
      {
        DiscountMatchType.UserId => x.Value == userId,
        DiscountMatchType.Role => roles.Contains(x.Value),
        _ => throw new ArgumentOutOfRangeException(),
      }
    );
  }
}
