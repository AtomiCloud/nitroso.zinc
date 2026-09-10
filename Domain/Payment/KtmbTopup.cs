using CSharp_Result;
using Microsoft.Extensions.Logging;

namespace Domain.Payment;

// The KTMB card top-ups: money we move onto the Airwallex issuing card to buy
// train tickets with. These are the LAST invoice input that was still collected
// by hand — an admin downloaded the issuing ledger to a file every month and
// transcribed the totals.
//
// They matter because they set the blended FX rate. A top-up is charged in MYR
// (transaction_amount) and billed to us in SGD (billing_amount); the ratio of
// the two over a month is the rate at which the invoice converts KTMB fares to
// SGD. It is a measured rate, not a quoted one, which is why the top-ups have
// to be stored rather than approximated from a market rate.
//
// Every figure here is as-reported. Nothing is netted, converted or rounded in
// this layer — the invoice engine decides how to blend them.

// how a top-up row got here
public enum KtmbTopupSource : byte
{
  // swept from the Airwallex issuing ledger (the normal path)
  Gateway = 0,

  // entered by an admin. The escape hatch for a top-up the sweep cannot see:
  // paid on a different card, or made before the issuing feed existed.
  Manual = 1,
}

// pure business data of one KTMB card top-up
public record KtmbTopupRecord
{
  // the gateway's issuing transaction id — globally unique, the upsert key
  public required string IssuingTransactionId { get; init; }

  // when the transaction posted (settled). Bucketed to an SGT calendar month
  // downstream. Airwallex reports posted_date for cleared transactions and
  // nothing for pending ones, so the adapter falls back to transaction_date.
  public required DateTime PostedAt { get; init; }

  // charged amount in MYR, always positive here. Airwallex reports card
  // spending as negative (money leaving); the adapter takes the absolute
  // value so a top-up reads as the positive quantity it is.
  public required decimal AmountMyr { get; init; }

  // what we were billed in SGD for that MYR, always positive
  public required decimal AmountSgd { get; init; }

  // the merchant string the gateway reported, kept verbatim for audit — this
  // is the field the KTMB filter matched on, so storing it lets a human check
  // the filter's decisions after the fact
  public required string MerchantName { get; init; }

  public required KtmbTopupSource Source { get; init; }
}

// one issuing transaction as the gateway reports it, already filtered to
// KTMB card spending
public record KtmbTopupLine
{
  public required string IssuingTransactionId { get; init; }

  public required DateTime PostedAt { get; init; }

  public required decimal AmountMyr { get; init; }

  public required decimal AmountSgd { get; init; }

  public required string MerchantName { get; init; }
}

// KTMB top-ups posted in [fromUtc, toUtc), straight from the gateway's
// issuing ledger
public interface IKtmbTopupSource
{
  Task<Result<IEnumerable<KtmbTopupLine>>> InRange(DateTime fromUtc, DateTime toUtc);
}

public interface IKtmbTopupRepository
{
  // idempotent by IssuingTransactionId: existing rows are refreshed, unseen
  // rows inserted (see KtmbTopupPlanner). Returns rows written.
  Task<Result<int>> Upsert(IEnumerable<KtmbTopupRecord> records);

  // latest PostedAt among stored GATEWAY-sourced rows — the sweep's
  // incremental watermark; null = never swept (sweep full history).
  //
  // Deliberately ignores Manual rows: an admin back-entering a top-up from
  // two years ago must not drag the watermark backwards, and one entered
  // with a future date must not skip the sweep past real transactions.
  Task<Result<DateTime?>> LatestGatewayPostedAt();
}

// Pure upsert planning, shared by the repository and the unit tests. Same
// shape as GatewayFeePlanner: rows whose IssuingTransactionId is already
// stored become updates, unseen ids become inserts, and duplicate ids within
// one batch collapse to the last occurrence — so replaying the same gateway
// response is a refresh, never a duplicate row.
public static class KtmbTopupPlanner
{
  public static (KtmbTopupRecord[] ToInsert, KtmbTopupRecord[] ToUpdate) Plan(
    IReadOnlyCollection<string> existingIds,
    IEnumerable<KtmbTopupRecord> incoming
  )
  {
    var deduped = incoming
      .GroupBy(x => x.IssuingTransactionId)
      .Select(g => g.Last())
      .ToArray();
    var existing = existingIds.ToHashSet();
    return (
      deduped.Where(x => !existing.Contains(x.IssuingTransactionId)).ToArray(),
      deduped.Where(x => existing.Contains(x.IssuingTransactionId)).ToArray()
    );
  }
}

// Pure window and mapping rules, shared by the sweep service and the unit
// tests. The filter constants are the load-bearing part: they are what
// separates a KTMB ticket purchase from every other card charge on the same
// account (Cloudflare, Grafana, Google, ACRA and so on all post to this
// ledger), and they are reproduced from the extractor that generated the
// issued June/July/August invoices.
public static class KtmbTopupSweepPlanner
{
  // re-read this much before the watermark every sweep: issuing transactions
  // post with delay and an AUTHORIZATION can be superseded by a later
  // CLEARING, so the overlap lets a corrected row overwrite an earlier one.
  // The idempotent upsert makes re-reading free.
  public static readonly TimeSpan Overlap = TimeSpan.FromDays(7);

  // first-run "full history" floor — before the Airwallex account existed,
  // so the first sweep captures every top-up ever made
  public static readonly DateTime FullHistoryStartUtc = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

  public static DateTime WindowStart(DateTime? watermarkUtc) =>
    watermarkUtc is { } w ? w - Overlap : FullHistoryStartUtc;

  // What a KTMB ticket purchase looks like in the issuing ledger. All four
  // conditions are required:
  //
  //   MYR       — tickets are bought in ringgit. Every other charge on this
  //               card is USD or SGD, so currency alone excludes most of them.
  //   APPROVED  — the ledger also carries FAILED attempts, which cost nothing
  //               and must not enter the FX blend.
  //   CLEARING  — an AUTHORIZATION is a hold at an estimated amount; the
  //               CLEARING that follows carries the amount actually billed.
  //               Counting both would double every top-up.
  //   KTMB      — merchant substring. Currently only "KTMB GO TICKETING"
  //               appears, but the merchant string is gateway-controlled and
  //               has changed before, so this matches loosely on purpose.
  //
  // Verified against production: applying exactly these to the live issuing
  // API for June 2026 yields RM 28,100.00 / SGD 8,985.64 — the figures on
  // issued invoice BB-2026-0601, to the cent.
  public const string TopupCurrency = "MYR";
  public const string ApprovedStatus = "APPROVED";
  public const string ClearingType = "CLEARING";
  public const string MerchantMatch = "KTMB";

  public static bool IsKtmbTopup(string? currency, string? status, string? type, string? merchant) =>
    string.Equals(currency, TopupCurrency, StringComparison.OrdinalIgnoreCase)
    && string.Equals(status, ApprovedStatus, StringComparison.OrdinalIgnoreCase)
    && string.Equals(type, ClearingType, StringComparison.OrdinalIgnoreCase)
    && merchant is not null
    && merchant.Contains(MerchantMatch, StringComparison.OrdinalIgnoreCase);

  public static KtmbTopupRecord ToRecord(KtmbTopupLine line) =>
    new()
    {
      IssuingTransactionId = line.IssuingTransactionId,
      PostedAt = line.PostedAt,
      // card spending posts negative (money leaving the account); a top-up is
      // naturally a positive quantity, so both amounts are normalised here
      // rather than at every read site
      AmountMyr = Math.Abs(line.AmountMyr),
      AmountSgd = Math.Abs(line.AmountSgd),
      MerchantName = line.MerchantName,
      Source = KtmbTopupSource.Gateway,
    };
}

public record KtmbTopupSweepReport
{
  public required DateTime FromUtc { get; init; }

  public required DateTime ToUtc { get; init; }

  // rows written (inserted or refreshed) this sweep
  public required int Wrote { get; init; }
}

// Sweep driver: full history on the first run (no gateway-sourced rows yet),
// then incremental from the stored watermark minus an overlap window for late
// postings — the idempotent upsert dedupes whatever the overlap re-reads.
// Mirrors GatewayAccountFeeSweep deliberately; the two run from the same
// worker tick and should fail and recover the same way.
public class KtmbTopupSweep(
  IKtmbTopupRepository repo,
  IKtmbTopupSource gateway,
  ILogger<KtmbTopupSweep> logger
)
{
  public async Task<Result<KtmbTopupSweepReport>> Sweep(DateTime nowUtc)
  {
    return await repo
      .LatestGatewayPostedAt()
      .ThenAwait(watermark =>
      {
        var from = KtmbTopupSweepPlanner.WindowStart(watermark);
        logger.LogInformation(
          "Sweeping KTMB card top-ups in [{From}, {To}) (watermark: {Watermark})",
          from,
          nowUtc,
          watermark
        );
        return gateway
          .InRange(from, nowUtc)
          .ThenAwait(lines => repo.Upsert(lines.Select(KtmbTopupSweepPlanner.ToRecord)))
          .Then(
            wrote => new KtmbTopupSweepReport
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
