using CSharp_Result;
using Domain.Timings;

namespace Domain.Booking;

// What BunnyBooker pays KTMB per ticket, per direction — an insert-only,
// effective-dated queue exactly like the withdrawal Fee queue: the newest
// row whose EffectiveAt has passed (per direction) is the live cost, future
// rows are the queue, and with no effective row the cost is ZERO (analysis
// then reports net without a KTMB component rather than guessing).
public record KtmbCostChange
{
  public required Guid Id { get; init; }

  public required TrainDirection Direction { get; init; }

  public required decimal Cost { get; init; }

  public required DateTime EffectiveAt { get; init; }

  public required DateTime CreatedAt { get; init; }
}

// current cost per direction + the queued future changes, for the admin UI
public record KtmbCostView
{
  // one entry per direction, 0m when never configured
  public required IReadOnlyDictionary<TrainDirection, decimal> Current { get; init; }

  public required KtmbCostChange[] Upcoming { get; init; }
}

public interface IKtmbCostRepository
{
  // the full queue (the table is tiny — a handful of admin-entered rows)
  Task<Result<IEnumerable<KtmbCostChange>>> List();

  Task<Result<KtmbCostChange>> Add(TrainDirection direction, decimal cost, DateTime? effectiveAt);
}

// Pure effective-dating math, shared by the endpoint and the unit tests.
// The analysis SQL implements the same newest-effective-first rule DB-side
// (per booking CompletedAt); this is the single in-memory source of truth.
public static class KtmbCostSchedule
{
  // the cost effective at a given instant for a direction; 0 when none
  public static decimal EffectiveCost(
    IEnumerable<KtmbCostChange> changes,
    TrainDirection direction,
    DateTime at
  ) =>
    changes
      .Where(x => x.Direction == direction && x.EffectiveAt <= at)
      .OrderByDescending(x => x.EffectiveAt)
      .ThenByDescending(x => x.CreatedAt)
      .ThenByDescending(x => x.Id)
      .Select(x => x.Cost)
      .FirstOrDefault();

  public static KtmbCostView View(IEnumerable<KtmbCostChange> changes, DateTime now)
  {
    var all = changes.ToArray();
    return new KtmbCostView
    {
      Current = new Dictionary<TrainDirection, decimal>
      {
        [TrainDirection.JToW] = EffectiveCost(all, TrainDirection.JToW, now),
        [TrainDirection.WToJ] = EffectiveCost(all, TrainDirection.WToJ, now),
      },
      Upcoming = all.Where(x => x.EffectiveAt > now).OrderBy(x => x.EffectiveAt).ToArray(),
    };
  }
}
