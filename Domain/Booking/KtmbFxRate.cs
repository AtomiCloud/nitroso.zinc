using CSharp_Result;

namespace Domain.Booking;

// The admin-entered MYR -> SGD conversion rate (SGD per 1 MYR) used to cost
// actual KTMB-paid amounts in the sales analysis — an insert-only,
// effective-dated queue exactly like the KtmbCosts estimate queue: the
// newest row whose EffectiveAt has passed is the live rate, future rows are
// the queue, and with no effective row there is NO rate (analysis then falls
// back to the per-direction estimate rather than guessing a conversion).
public record KtmbFxRateChange
{
  public required Guid Id { get; init; }

  // SGD per 1 MYR
  public required decimal Rate { get; init; }

  public required DateTime EffectiveAt { get; init; }

  public required DateTime CreatedAt { get; init; }
}

// the rate in effect right now + queued future changes + the recent history,
// for the admin UI (Current null = never configured)
public record KtmbFxRateView
{
  public required decimal? Current { get; init; }

  public required KtmbFxRateChange[] Upcoming { get; init; }

  // already-effective rows, newest first (the table is tiny — a handful of
  // admin-entered rows)
  public required KtmbFxRateChange[] History { get; init; }
}

public interface IKtmbFxRateRepository
{
  // the full queue (tiny, admin-entered)
  Task<Result<IEnumerable<KtmbFxRateChange>>> List();

  Task<Result<KtmbFxRateChange>> Add(decimal rate, DateTime? effectiveAt);
}

// Pure effective-dating math, shared by the endpoint and the unit tests.
// The analysis SQL implements the same newest-effective-first rule DB-side
// (per booking CompletedAt); this is the single in-memory source of truth.
public static class KtmbFxRateSchedule
{
  // the rate effective at a given instant; null when none has taken effect
  public static decimal? EffectiveRate(IEnumerable<KtmbFxRateChange> changes, DateTime at) =>
    changes
      .Where(x => x.EffectiveAt <= at)
      .OrderByDescending(x => x.EffectiveAt)
      .ThenByDescending(x => x.CreatedAt)
      .ThenByDescending(x => x.Id)
      .Select(x => (decimal?)x.Rate)
      .FirstOrDefault();

  public static KtmbFxRateView View(IEnumerable<KtmbFxRateChange> changes, DateTime now)
  {
    var all = changes.ToArray();
    return new KtmbFxRateView
    {
      Current = EffectiveRate(all, now),
      Upcoming = [.. all.Where(x => x.EffectiveAt > now).OrderBy(x => x.EffectiveAt)],
      History =
      [
        .. all
          .Where(x => x.EffectiveAt <= now)
          .OrderByDescending(x => x.EffectiveAt)
          .ThenByDescending(x => x.CreatedAt)
          .ThenByDescending(x => x.Id),
      ],
    };
  }
}

// Pure per-booking costing rule, shared by the analysis SQL (its CASE
// expression mirrors this) and the unit tests: the actual KTMB-paid amount
// when recorded — SGD as-is, MYR × the rate effective at the booking's
// CompletedAt — else the per-direction estimate. A recorded MYR amount with
// no effective rate falls back to the estimate for that booking rather than
// guessing a conversion.
public static class KtmbActualCost
{
  public const string Myr = "MYR";

  public const string Sgd = "SGD";

  public static decimal Effective(
    decimal? amount,
    string? currency,
    decimal? fxRate,
    decimal estimate
  ) =>
    amount switch
    {
      null => estimate,
      _ when currency == Sgd => amount.Value,
      _ when currency == Myr && fxRate != null => amount.Value * fxRate.Value,
      _ => estimate,
    };
}
