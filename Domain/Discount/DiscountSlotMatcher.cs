using Domain.Cost;

namespace Domain.Discount;

// Pure slot-dimension matching for discounts, mirroring
// Domain.Cost.CostPolicyMatcher.Applies for exact slot dimensions and the
// half-open effective window. Lead time intentionally points the other way:
// discounts reward buying at least N hours before departure.
public static class DiscountSlotMatcher
{
  public static bool HasSlotMatcher(DiscountRecord record) =>
    record.MatchDate != null
    || record.MatchTime != null
    || record.MatchDayOfWeek != null
    || record.MatchDirection != null
    || record.LeadTimeAtLeastHours != null;

  public static bool Applies(DiscountRecord record, BookingCostSpec? spec, DateTime nowUtc)
  {
    if (record.EffectiveAt != null && nowUtc < record.EffectiveAt)
      return false;
    if (record.ExpiresAt != null && nowUtc >= record.ExpiresAt)
      return false;

    // conservative: a spec-less price (Cost/self) is not slot-specific, so a
    // discount with ANY slot matcher set must never apply to it
    if (spec == null)
      return !HasSlotMatcher(record);

    if (record.MatchDate != null && record.MatchDate != spec.Date)
      return false;
    if (record.MatchTime != null && record.MatchTime != spec.Time)
      return false;
    if (record.MatchDayOfWeek != null && record.MatchDayOfWeek != spec.Date.DayOfWeek)
      return false;
    if (record.MatchDirection != null && record.MatchDirection != spec.Direction)
      return false;

    if (record.LeadTimeAtLeastHours != null)
    {
      var lead = CostPolicyMatcher.DepartureUtc(spec) - nowUtc;
      // At the threshold the customer has bought early enough, so the lower
      // bound is inclusive. A departed slot has no lead time to reward.
      if (lead <= TimeSpan.Zero || lead.TotalHours < record.LeadTimeAtLeastHours.Value)
        return false;
    }

    return true;
  }
}
