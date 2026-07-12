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
  // money movements in the (UTC instant) range that have no fee rows yet,
  // at most max entries
  Task<Result<IEnumerable<PendingFeeSource>>> ListPendingSources(
    DateTime after,
    DateTime before,
    int max
  );

  // idempotent by FinancialTransactionId: existing rows are refreshed,
  // unseen rows inserted (see GatewayFeePlanner). Returns rows written.
  Task<Result<int>> Upsert(IEnumerable<GatewayFeeRecord> records);
}

// fee lines for one source id, straight from the gateway; empty = the
// gateway has no financial transactions for it yet (they post with delay)
public interface IGatewayFeeSource
{
  Task<Result<IEnumerable<GatewayFeeLine>>> BySource(string sourceId);
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

    var pending = await repo.ListPendingSources(afterUtc, beforeUtc, MaxSourcesPerSync + 1);
    if (!pending.IsSuccess())
      return pending.FailureOrDefault();

    var all = pending.SuccessOrDefault().ToArray();
    var hasMore = all.Length > MaxSourcesPerSync;
    var batch = all.Take(MaxSourcesPerSync).ToArray();

    var synced = 0;
    var missing = new List<string>();
    foreach (var source in batch)
    {
      var lines = await gateway.BySource(source.SourceId);
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
