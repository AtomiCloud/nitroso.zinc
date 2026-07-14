using CSharp_Result;
using Domain.Payment;

namespace App.Modules.Payments.Airwallex;

// IGatewayFeeSource over the Airwallex financial_transactions API, mapped to
// source-type-agnostic fee lines. An empty answer means the gateway has not
// posted fees for the movement yet.
//
// Transfers and refunds are queried by their own id, but Airwallex keys a
// PAYMENT's financial transactions by the payment ATTEMPT id (att_...), not
// the intent id we store — querying by intent id always comes back empty. So
// payments first resolve intent -> latest_payment_attempt.id and query by
// that. The attempt id is only the gateway query key: the sync service still
// records fees under the original source id, keeping DB joins to Payments
// intact.
public class AirwallexGatewayFeeSource(AirWallexClient client) : IGatewayFeeSource
{
  public Task<Result<IEnumerable<GatewayFeeLine>>> BySource(PendingFeeSource source)
  {
    return this.GatewayQueryId(source)
      .ThenAwait(queryId =>
        queryId is null
          // no attempt to query yet (intent unknown to the gateway or never
          // attempted) — same shape as "fees not posted yet": the source
          // stays pending and a later sync retries it
          ? Task.FromResult(Array.Empty<AirwallexFinancialTransactionRes>().ToResult())
          : client.ListFinancialTransactionsBySource(queryId)
      )
      .Then(
        items =>
          items.Select(x => new GatewayFeeLine
          {
            FinancialTransactionId = x.Id,
            Amount = x.Amount,
            Fee = x.Fee,
            Net = x.Net,
            Currency = x.Currency,
            TransactedAt = DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc),
          }),
        Errors.MapNone
      );
  }

  private Task<Result<string?>> GatewayQueryId(PendingFeeSource source) =>
    source.SourceType is GatewayFeeSourceType.Payment
      ? client
        .GetPaymentIntent(source.SourceId)
        .Then(intent => intent?.LatestPaymentAttempt?.Id, Errors.MapNone)
      : Task.FromResult((Result<string?>)source.SourceId);
}
