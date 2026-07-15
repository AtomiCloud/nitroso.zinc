using Domain.Booking;
using Domain.User;

namespace Domain;

// Owner-only history gate for the analysis/P&L read models: callers without
// the 'owner' role may only see data from June 2026 (SGT) onward. The gate is
// a server-side CLAMP, never an error — the effective After becomes
// max(requested After, NonOwnerFloor); Before is left alone, so a range that
// ends before the floor turns into an empty range (After > Before) and every
// repository/calculator naturally returns nothing for it.
public static class RangeClamp
{
  // first SGT calendar date non-owners may see
  public static readonly DateOnly NonOwnerFloor = new(2026, 6, 1);

  public static (DateOnly? After, DateOnly? Before) Clamp(
    DateOnly? after,
    DateOnly? before,
    bool fullHistory
  ) =>
    fullHistory
      ? (after, before)
      : (after is null || after < NonOwnerFloor ? NonOwnerFloor : after, before);

  public static BookingAnalysisQuery ClampHistory(this BookingAnalysisQuery q, bool fullHistory)
  {
    var (after, before) = Clamp(q.After, q.Before, fullHistory);
    return q with { After = after, Before = before };
  }

  public static TravelAnalysisQuery ClampHistory(this TravelAnalysisQuery q, bool fullHistory)
  {
    var (after, before) = Clamp(q.After, q.Before, fullHistory);
    return q with { After = after, Before = before };
  }

  public static ProfitAnalysisQuery ClampHistory(this ProfitAnalysisQuery q, bool fullHistory)
  {
    var (after, before) = Clamp(q.After, q.Before, fullHistory);
    return q with { After = after, Before = before };
  }

  public static PnlAnalysisQuery ClampHistory(this PnlAnalysisQuery q, bool fullHistory)
  {
    var (after, before) = Clamp(q.After, q.Before, fullHistory);
    return q with { After = after, Before = before };
  }

  public static PnlTerminalQuery ClampHistory(this PnlTerminalQuery q, bool fullHistory)
  {
    var (after, before) = Clamp(q.After, q.Before, fullHistory);
    return q with { After = after, Before = before };
  }

  public static BookingBoostQuery ClampHistory(this BookingBoostQuery q, bool fullHistory)
  {
    var (after, before) = Clamp(q.After, q.Before, fullHistory);
    return q with { After = after, Before = before };
  }

  public static PartnerPnlQuery ClampHistory(this PartnerPnlQuery q, bool fullHistory)
  {
    var (after, before) = Clamp(q.After, q.Before, fullHistory);
    return q with { After = after, Before = before };
  }
}
