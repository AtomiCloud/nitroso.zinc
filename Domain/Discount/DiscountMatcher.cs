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
    // the user/role semantics live in TargetMatcher, shared with the
    // priority-boost targeting so the two features can never drift
    return TargetMatcher.Matches(target, userId, roles);
  }
}
