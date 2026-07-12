using CSharp_Result;
using Domain.Payment;

namespace App.Modules.Payments.Airwallex;

// IGatewayFeeSource over the Airwallex financial_transactions API: one call
// per source id, mapped to source-type-agnostic fee lines. An empty answer
// means the gateway has not posted fees for the movement yet.
public class AirwallexGatewayFeeSource(AirWallexClient client) : IGatewayFeeSource
{
  public Task<Result<IEnumerable<GatewayFeeLine>>> BySource(string sourceId)
  {
    return client
      .ListFinancialTransactionsBySource(sourceId)
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
}
