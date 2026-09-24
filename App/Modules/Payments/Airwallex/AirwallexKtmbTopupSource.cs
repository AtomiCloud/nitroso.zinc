using CSharp_Result;
using Domain.Payment;

namespace App.Modules.Payments.Airwallex;

// IKtmbTopupSource over the Airwallex issuing API: card transactions in a
// created-at window, filtered down to KTMB ticket purchases.
//
// The filter is NOT applied by the gateway — the issuing endpoint has no
// merchant or currency parameter, so it returns every card charge on the
// account (Cloudflare, Grafana, Google, ACRA, Apple and so on) and the
// narrowing happens here. That makes KtmbTopupSweepPlanner.IsKtmbTopup the
// only thing standing between an unrelated SaaS invoice and the invoice's FX
// rate, which is why it lives in the domain with the reasoning attached
// rather than being inlined as a LINQ predicate.
public class AirwallexKtmbTopupSource(AirWallexClient client) : IKtmbTopupSource
{
  public Task<Result<IEnumerable<KtmbTopupLine>>> InRange(DateTime fromUtc, DateTime toUtc)
  {
    return client
      .ListIssuingTransactions(fromUtc, toUtc)
      .Then(
        items =>
          items
            .Where(x =>
              KtmbTopupSweepPlanner.IsKtmbTopup(
                x.TransactionCurrency,
                x.Status,
                x.TransactionType,
                x.Merchant?.Name
              )
            )
            .Select(x => new KtmbTopupLine
            {
              IssuingTransactionId = x.TransactionId,
              // posted_date is the settlement time and the one the invoice
              // buckets on. It has been present on every production row, but
              // the gateway documents it as absent before clearing, so an
              // uncleared row falls back to when the card was presented
              // rather than being dropped or dated to the epoch.
              PostedAt = DateTime.SpecifyKind(
                x.PostedDate ?? x.TransactionDate ?? DateTime.UtcNow,
                DateTimeKind.Utc
              ),
              AmountMyr = x.TransactionAmount,
              AmountSgd = x.BillingAmount,
              MerchantName = x.Merchant?.Name ?? string.Empty,
            }),
        Errors.MapNone
      );
  }
}
