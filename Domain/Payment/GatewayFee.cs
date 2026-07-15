using CSharp_Result;
using Microsoft.Extensions.Logging;

namespace Domain.Payment;

// Airwallex's own per-movement fees ("financial transactions"), captured
// after the fact for the admin Analysis page. Each row is one gateway
// financial transaction keyed by its FinancialTransactionId (the idempotent
// upsert key); SourceId ties it back to the object that moved the money —
// a payment intent, a payout transfer or a card refund.
public enum GatewayFeeSourceType : byte
{
  // a captured payment intent (PaymentData.ExternalReference)
  Payment = 0,

  // a PayNow payout transfer (WithdrawalData.ConfirmationNumber)
  Transfer = 1,

  // a card-refund fragment (WithdrawalRefundData.AirwallexRefundId)
  Refund = 2,

  // an account-level FEE financial transaction with no owning money movement
  // in our DB: Airwallex bills these as aggregate FEE-type rows against an
  // internal source (SourceId = the transaction's reported source_id). A
  // production sweep showed the batches are DOMINATED by per-payment-attempt
  // fees (gateway + 3DS on every checkout attempt, including failed ones),
  // with per-refund fees a small minority — so they are a cost of ACCEPTING
  // DEPOSITS and count as payment-side fees in the analysis views (see
  // GatewayFeeBuckets). Captured by the account-fee sweep.
  AccountFee = 3,
}

// Which side of the money flow a fee row belongs to in the analysis views.
// Payment-side fees are costs of accepting deposits and feed the blended
// gateway rate (gwRate = paymentFees / deposits); payout-side fees are costs
// of moving money out (paid per transfer / card refund).
public static class GatewayFeeBuckets
{
  // paymentFees = Payment + AccountFee (account-level billings are dominated
  // by per-attempt gateway/3DS fees — deposit acceptance costs)
  public static bool IsPaymentSide(GatewayFeeSourceType type) =>
    type is GatewayFeeSourceType.Payment or GatewayFeeSourceType.AccountFee;

  // payoutFees = Transfer + Refund ONLY
  public static bool IsPayoutSide(GatewayFeeSourceType type) =>
    type is GatewayFeeSourceType.Transfer or GatewayFeeSourceType.Refund;
}

// pure business data of one gateway financial transaction
public record GatewayFeeRecord
{
  // the intent/transfer/refund id the gateway reported this row against
  public required string SourceId { get; init; }

  public required GatewayFeeSourceType SourceType { get; init; }

  // the gateway's financial transaction id — globally unique, the upsert key
  public required string FinancialTransactionId { get; init; }

  public required decimal Amount { get; init; }

  public required decimal Fee { get; init; }

  public required decimal Net { get; init; }

  public required string Currency { get; init; }

  public required DateTime TransactedAt { get; init; }
}

// one fee line as the gateway reports it (source-type-agnostic — the caller
// knows which source it asked about)
public record GatewayFeeLine
{
  public required string FinancialTransactionId { get; init; }

  public required decimal Amount { get; init; }

  public required decimal Fee { get; init; }

  public required decimal Net { get; init; }

  public required string Currency { get; init; }

  public required DateTime TransactedAt { get; init; }
}

// a money movement in range that has no fee rows yet — the sync worklist
public record PendingFeeSource
{
  public required string SourceId { get; init; }

  public required GatewayFeeSourceType SourceType { get; init; }
}

public record GatewayFeeSyncQuery
{
  // inclusive SGT calendar dates (same convention as the analysis range);
  // null = unbounded
  public DateOnly? After { get; init; }

  public DateOnly? Before { get; init; }

  // source ids to skip this call — the recurring runner feeds back the ids
  // it already found missing this run, so consecutive batches advance past
  // them instead of re-listing the same head of the worklist forever
  public IReadOnlyCollection<string> ExcludeSourceIds { get; init; } = [];
}

public record GatewayFeeSyncResult
{
  // sources that gained (or refreshed) fee rows this call
  public required int Synced { get; init; }

  // sources still without fee data — the gateway had no rows yet (fees post
  // with delay) or the lookup failed; retry on a later sync
  public required string[] Missing { get; init; }

  // more pending sources exist beyond the per-call bound — call again
  public required bool HasMore { get; init; }
}

public interface IGatewayFeeRepository
{
  // money movements in the (UTC instant) range that have no fee rows yet
  // and are not in exclude, at most max entries
  Task<Result<IEnumerable<PendingFeeSource>>> ListPendingSources(
    DateTime after,
    DateTime before,
    IReadOnlyCollection<string> exclude,
    int max
  );

  // idempotent by FinancialTransactionId: existing rows are refreshed,
  // unseen rows inserted (see GatewayFeePlanner). Returns rows written.
  Task<Result<int>> Upsert(IEnumerable<GatewayFeeRecord> records);

  // latest TransactedAt among stored AccountFee rows — the account-fee
  // sweep's incremental watermark; null = never swept (sweep full history)
  Task<Result<DateTime?>> LatestAccountFeeTransactedAt();
}

// fee lines for one pending source, straight from the gateway; empty = the
// gateway has no financial transactions for it yet (they post with delay).
// Takes the full source (not just the id) because the gateway may key its
// ledger differently per source type — e.g. Airwallex keys a payment's rows
// by the payment attempt id, which the adapter resolves from the intent id.
public interface IGatewayFeeSource
{
  Task<Result<IEnumerable<GatewayFeeLine>>> BySource(PendingFeeSource source);
}

public interface IGatewayFeeSyncService
{
  Task<Result<GatewayFeeSyncResult>> Sync(GatewayFeeSyncQuery query);
}

// Pure upsert planning, shared by the repository and the unit tests: rows
// whose FinancialTransactionId is already stored become updates, unseen ids
// become inserts, and duplicate ids within one batch collapse to the last
// occurrence — so replaying the same gateway response is a no-op-shaped
// refresh, never a duplicate row.
public static class GatewayFeePlanner
{
  public static (GatewayFeeRecord[] ToInsert, GatewayFeeRecord[] ToUpdate) Plan(
    IReadOnlyCollection<string> existingIds,
    IEnumerable<GatewayFeeRecord> incoming
  )
  {
    var deduped = incoming
      .GroupBy(x => x.FinancialTransactionId)
      .Select(g => g.Last())
      .ToArray();
    var existing = existingIds.ToHashSet();
    return (
      deduped.Where(x => !existing.Contains(x.FinancialTransactionId)).ToArray(),
      deduped.Where(x => existing.Contains(x.FinancialTransactionId)).ToArray()
    );
  }
}

// Sync driver: list the movements in range that still lack fee rows, ask the
// gateway for each one's financial transactions and upsert what it returns.
// A source with no rows yet (gateway fees post with delay) or a failed
// lookup stays in Missing and is retried by a later sync — one flaky source
// never fails the batch.
public class GatewayFeeSyncService(
  IGatewayFeeRepository repo,
  IGatewayFeeSource gateway,
  ILogger<GatewayFeeSyncService> logger
) : IGatewayFeeSyncService
{
  // bound the per-call work so an admin backfilling months of history gets
  // fast, resumable batches instead of one giant request
  public const int MaxSourcesPerSync = 200;

  public async Task<Result<GatewayFeeSyncResult>> Sync(GatewayFeeSyncQuery query)
  {
    var sgt = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
    // inclusive SGT dates -> half-open UTC instant range [after, before)
    var afterUtc =
      query.After?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified) is { } a
        ? TimeZoneInfo.ConvertTimeToUtc(a, sgt)
        : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    var beforeUtc =
      query.Before?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified) is { } b
        ? TimeZoneInfo.ConvertTimeToUtc(b, sgt)
        : DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

    var pending = await repo.ListPendingSources(
      afterUtc,
      beforeUtc,
      query.ExcludeSourceIds,
      MaxSourcesPerSync + 1
    );
    if (!pending.IsSuccess())
      return pending.FailureOrDefault();

    var all = pending.SuccessOrDefault().ToArray();
    var hasMore = all.Length > MaxSourcesPerSync;
    var batch = all.Take(MaxSourcesPerSync).ToArray();

    var synced = 0;
    var missing = new List<string>();
    foreach (var source in batch)
    {
      var lines = await gateway.BySource(source);
      if (!lines.IsSuccess())
      {
        // transient gateway trouble: leave the source for the next sync
        logger.LogWarning(
          "Gateway fee lookup failed for {SourceType} '{SourceId}': {Error}",
          source.SourceType,
          source.SourceId,
          lines.FailureOrDefault().Message
        );
        missing.Add(source.SourceId);
        continue;
      }

      var records = lines
        .SuccessOrDefault()
        .Select(l => new GatewayFeeRecord
        {
          SourceId = source.SourceId,
          SourceType = source.SourceType,
          FinancialTransactionId = l.FinancialTransactionId,
          Amount = l.Amount,
          Fee = l.Fee,
          Net = l.Net,
          Currency = l.Currency,
          TransactedAt = l.TransactedAt,
        })
        .ToArray();
      if (records.Length == 0)
      {
        // fees post with delay — not yet available, stays pending
        missing.Add(source.SourceId);
        continue;
      }

      var wrote = await repo.Upsert(records);
      if (!wrote.IsSuccess())
        return wrote.FailureOrDefault();
      synced++;
    }

    return new GatewayFeeSyncResult
    {
      Synced = synced,
      Missing = missing.ToArray(),
      HasMore = hasMore,
    };
  }
}

public record GatewayFeeSyncRunReport
{
  // Sync batches executed this run
  public required int Batches { get; init; }

  // total sources that gained (or refreshed) fee rows across all batches
  public required int Synced { get; init; }

  // distinct sources found still without fee data this run — fees post with
  // delay, so they stay pending and the next run retries them
  public required int Missing { get; init; }
}

// Drains the pending-fee backlog for the recurring sync worker: run Sync
// with an unbounded range repeatedly while more pending sources exist,
// bounded by maxBatches so one run can never spin forever. Sources a batch
// reports missing are excluded from the run's later batches — the worklist
// is ordered, so without that feedback a head of permanently-missing
// sources would be re-listed every batch and starve everything behind it.
// The next run naturally retries them (fees post with delay). Cancellation
// is honoured between batches.
public class GatewayFeeSyncRunner(
  IGatewayFeeSyncService service,
  ILogger<GatewayFeeSyncRunner> logger
)
{
  public const int MaxBatchesPerRun = 30;

  public async Task<Result<GatewayFeeSyncRunReport>> Drain(
    int maxBatches,
    CancellationToken ct = default
  )
  {
    var batches = 0;
    var synced = 0;
    var missing = new HashSet<string>();
    while (batches < maxBatches && !ct.IsCancellationRequested)
    {
      var r = await service.Sync(new GatewayFeeSyncQuery { ExcludeSourceIds = missing.ToArray() });
      if (!r.IsSuccess())
        return r.FailureOrDefault();

      var result = r.SuccessOrDefault();
      batches++;
      synced += result.Synced;
      missing.UnionWith(result.Missing);
      logger.LogInformation(
        "Gateway fee sync batch {Batch}: {Synced} synced, {Missing} missing, hasMore: {HasMore}",
        batches,
        result.Synced,
        result.Missing.Length,
        result.HasMore
      );
      if (!result.HasMore)
        break;
    }

    return new GatewayFeeSyncRunReport
    {
      Batches = batches,
      Synced = synced,
      Missing = missing.Count,
    };
  }
}

// ---- account-level fee sweep ----
//
// The per-source sync above only finds fees reported against movements we
// know (intents, transfers, refunds). Airwallex also bills account-level
// fees as aggregate FEE-type transactions with no owning source we track —
// dominated by per-payment-attempt fees (gateway + 3DS on every checkout
// attempt, including failed ones), plus smaller items like the S$0.30
// card-refund fee (which never lands on the refund's own financial
// transaction; fee = 0 there). This sweep lists FEE-type financial
// transactions over a created-at window and upserts them as
// SourceType = AccountFee.

// one account-level FEE-type financial transaction as the gateway reports it
public record GatewayAccountFeeLine
{
  // the gateway's own source_id for the fee (an internal billing object,
  // not a movement we track); empty when the gateway reports none
  public required string SourceId { get; init; }

  public required string FinancialTransactionId { get; init; }

  public required decimal Amount { get; init; }

  public required decimal Net { get; init; }

  public required string Currency { get; init; }

  public required DateTime TransactedAt { get; init; }
}

// FEE-type financial transactions created in [fromUtc, toUtc), straight from
// the gateway's ledger
public interface IGatewayAccountFeeSource
{
  Task<Result<IEnumerable<GatewayAccountFeeLine>>> InRange(DateTime fromUtc, DateTime toUtc);
}

// Pure window/mapping rules, shared by the sweep service and the unit tests.
public static class GatewayAccountFeePlanner
{
  // re-read this much before the watermark every sweep: financial
  // transactions can post with delay, and the idempotent upsert makes the
  // overlap a refresh, never a duplicate
  public static readonly TimeSpan Overlap = TimeSpan.FromDays(3);

  // first-run "full history" floor — safely before the Airwallex account
  // existed, so the first sweep captures everything ever billed
  public static readonly DateTime FullHistoryStartUtc = new(
    2020,
    1,
    1,
    0,
    0,
    0,
    DateTimeKind.Utc
  );

  public static DateTime WindowStart(DateTime? watermarkUtc) =>
    watermarkUtc is { } w ? w - Overlap : FullHistoryStartUtc;

  // account fees post as negative amounts (money leaving the account); the
  // stored Fee is the positive fee take, Amount/Net stay as reported
  public static GatewayFeeRecord ToRecord(GatewayAccountFeeLine line) =>
    new()
    {
      SourceId = line.SourceId,
      SourceType = GatewayFeeSourceType.AccountFee,
      FinancialTransactionId = line.FinancialTransactionId,
      Amount = line.Amount,
      Fee = Math.Abs(line.Amount),
      Net = line.Net,
      Currency = line.Currency,
      TransactedAt = line.TransactedAt,
    };
}

public record GatewayAccountFeeSweepReport
{
  public required DateTime FromUtc { get; init; }

  public required DateTime ToUtc { get; init; }

  // rows written (inserted or refreshed) this sweep
  public required int Wrote { get; init; }
}

// Sweep driver: full history on the first run (no AccountFee rows yet), then
// incremental from the stored watermark minus an overlap window for late
// postings — the idempotent upsert dedupes whatever the overlap re-reads.
public class GatewayAccountFeeSweep(
  IGatewayFeeRepository repo,
  IGatewayAccountFeeSource gateway,
  ILogger<GatewayAccountFeeSweep> logger
)
{
  public async Task<Result<GatewayAccountFeeSweepReport>> Sweep(DateTime nowUtc)
  {
    return await repo
      .LatestAccountFeeTransactedAt()
      .ThenAwait(watermark =>
      {
        var from = GatewayAccountFeePlanner.WindowStart(watermark);
        logger.LogInformation(
          "Sweeping account-level gateway fees in [{From}, {To}) (watermark: {Watermark})",
          from,
          nowUtc,
          watermark
        );
        return gateway
          .InRange(from, nowUtc)
          .ThenAwait(lines => repo.Upsert(lines.Select(GatewayAccountFeePlanner.ToRecord)))
          .Then(
            wrote => new GatewayAccountFeeSweepReport
            {
              FromUtc = from,
              ToUtc = nowUtc,
              Wrote = wrote,
            },
            Errors.MapNone
          );
      });
  }
}
