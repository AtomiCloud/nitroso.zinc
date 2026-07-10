namespace Domain.Cost;

// Pure policy matching rules, shared by pricing and (unit) tests
public static class CostPolicyMatcher
{
  // Booking Date + Time are SGT (UTC+8) wall clock; see BookingStats for the
  // established departure-instant pattern
  public static DateTime DepartureUtc(BookingCostSpec spec) =>
    spec.Date.ToDateTime(spec.Time, DateTimeKind.Unspecified).AddHours(-8);

  // A policy applies iff it is enabled, nowUtc is within its active window
  // [EffectiveAt, ExpiresAt), every non-null Match* dimension equals the
  // booking's, and the purchase-to-departure lead time is strictly under the cap
  public static bool Applies(CostPolicyRecord policy, BookingCostSpec spec, DateTime nowUtc)
  {
    if (!policy.Enabled)
      return false;

    if (policy.EffectiveAt != null && nowUtc < policy.EffectiveAt)
      return false;
    if (policy.ExpiresAt != null && nowUtc >= policy.ExpiresAt)
      return false;

    if (policy.MatchDate != null && policy.MatchDate != spec.Date)
      return false;
    if (policy.MatchTime != null && policy.MatchTime != spec.Time)
      return false;
    if (policy.MatchDayOfWeek != null && policy.MatchDayOfWeek != spec.Date.DayOfWeek)
      return false;
    if (policy.MatchDirection != null && policy.MatchDirection != spec.Direction)
      return false;

    if (policy.LeadTimeUnderHours != null)
    {
      var lead = DepartureUtc(spec) - nowUtc;
      // a departed slot has no lead time at all — an "under N hours"
      // surcharge must not match bookings for the past
      if (lead <= TimeSpan.Zero || lead.TotalHours >= policy.LeadTimeUnderHours.Value)
        return false;
    }

    return true;
  }
}
