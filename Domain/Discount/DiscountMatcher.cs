using Domain.Cost;
using Microsoft.Extensions.Logging;

namespace Domain.Discount;

public interface IDiscountMatcher
{
  // user/role targeting via the Target blob AND slot targeting via the
  // record's slot matchers (spec = null is the non-slot-specific Cost/self
  // price: any slot-targeted discount conservatively never matches it)
  bool Match(
    DiscountTarget target,
    DiscountRecord record,
    string userId,
    string[] roles,
    BookingCostSpec? spec,
    DateTime nowUtc
  );
}

public class DiscountMatcher(ILogger<DiscountMatcher> logger) : IDiscountMatcher
{
  public bool Match(
    DiscountTarget target,
    DiscountRecord record,
    string userId,
    string[] roles,
    BookingCostSpec? spec,
    DateTime nowUtc
  )
  {
    logger.LogInformation(
      "Matching: {Type} {@Values} for {@UserId} with {@Roles} against {@Spec}",
      target.MatchMode,
      target.Matches.ToArray(),
      userId,
      roles,
      spec
    );
    if (!DiscountSlotMatcher.Applies(record, spec, nowUtc))
      return false;
    return target.MatchMode switch
    {
      DiscountMatchMode.All => this.MatchAll(target.Matches, userId, roles),
      DiscountMatchMode.Any => this.MatchAny(target.Matches, userId, roles),
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
