using CSharp_Result;
using Microsoft.Extensions.Logging;

namespace Domain.Withdrawal;

// What one drain accomplished, for the worker's log line
public record RefundArnBackfillReport
{
  public required int Batches { get; init; }

  // fragments looked up at the gateway this run
  public required int Scanned { get; init; }

  // fragments that gained an ARN
  public required int Captured { get; init; }

  // looked up, still no ARN — settled but the network has not published one
  // yet, or the gateway no longer knows the refund. Retried next tick.
  public required int Pending { get; init; }

  // settled fragments with no ARN that sit beyond the retention horizon: the
  // gateway will never answer for them, so they are excluded from the backlog
  // entirely. Reported so the permanent gap in the export is visible.
  public required int Unbackfillable { get; init; }
}

// Backfills the Acquirer Reference Number of settled card-refund fragments
// that predate ARN capture (and of any settled event that arrived without
// one). Shaped like GatewayFeeSyncRunner: bounded batches of sequential
// gateway lookups per run, so the first runs after a deploy drain history
// over a few hours without ever spiking the gateway.
public class RefundArnBackfillRunner(
  IWithdrawalRefundRepository refundRepo,
  IRefundGateway gateway,
  ILogger<RefundArnBackfillRunner> logger
)
{
  public const int BatchSize = 50;

  public const int MaxBatchesPerRun = 20;

  // Airwallex keeps a refund queryable for at most 2 YEARS since creation;
  // past that the lookup returns nothing and no ARN can ever be recovered.
  // A naive "still missing an ARN" backlog would therefore re-query those
  // rows on every tick forever, so the backlog query is bounded by fragment
  // age and they fall out of it instead. The 30-day haircut keeps us off the
  // cliff edge: a row is dropped shortly before the gateway stops answering,
  // rather than being retried right up to a boundary we cannot observe.
  public static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(365 * 2 - 30);

  public async Task<Result<RefundArnBackfillReport>> Drain(
    DateTime nowUtc,
    int maxBatches,
    CancellationToken ct = default
  )
  {
    var horizon = nowUtc - RetentionWindow;

    var unbackfillableR = await refundRepo.CountUnbackfillableArn(horizon);
    if (!unbackfillableR.IsSuccess())
      return unbackfillableR.FailureOrDefault();
    var unbackfillable = unbackfillableR.SuccessOrDefault();

    var batches = 0;
    var scanned = 0;
    var captured = 0;
    // fragments this run already asked about and got no ARN for. Without
    // this the next batch query would return the same rows again and the
    // drain would spin on them instead of advancing through the backlog.
    var pending = new HashSet<Guid>();

    while (batches < maxBatches && !ct.IsCancellationRequested)
    {
      var backlogR = await refundRepo.ListSettledMissingArn(horizon, pending, BatchSize);
      if (!backlogR.IsSuccess())
        return backlogR.FailureOrDefault();
      var backlog = backlogR.SuccessOrDefault();
      if (backlog.Count == 0)
        break;

      batches++;
      foreach (var fragment in backlog)
      {
        if (ct.IsCancellationRequested)
          break;
        // guarded by the query, but the compiler does not know that
        if (fragment.AirwallexRefundId == null)
          continue;

        scanned++;
        var lookup = await gateway.GetRefundStatus(fragment.AirwallexRefundId);
        if (!lookup.IsSuccess())
        {
          // a transport blip must not abort the drain: skip the row for this
          // run and let the next tick retry it
          logger.LogWarning(
            lookup.FailureOrDefault(),
            "ARN backfill: lookup failed for refund '{RefundId}' (fragment '{Id}'); retrying next tick",
            fragment.AirwallexRefundId,
            fragment.Id
          );
          pending.Add(fragment.Id);
          continue;
        }

        var arn = lookup.SuccessOrDefault().AcquirerReferenceNumber;
        if (arn == null)
        {
          pending.Add(fragment.Id);
          continue;
        }

        // status-only-null update: this writes the ARN and nothing else, so
        // a backfill can never disturb a fragment's settlement bookkeeping
        var stored = await refundRepo.Update(fragment.Id, null, null, null, arn);
        if (!stored.IsSuccess())
        {
          logger.LogWarning(
            stored.FailureOrDefault(),
            "ARN backfill: failed storing ARN for fragment '{Id}'; retrying next tick",
            fragment.Id
          );
          pending.Add(fragment.Id);
          continue;
        }

        captured++;
      }

      // the batch was fully absorbed into `pending` without a single
      // capture: the remaining backlog is all rows we just asked about, so
      // another query this run would return nothing new
      if (backlog.Count < BatchSize)
        break;
    }

    return new RefundArnBackfillReport
    {
      Batches = batches,
      Scanned = scanned,
      Captured = captured,
      Pending = pending.Count,
      Unbackfillable = unbackfillable,
    };
  }
}
