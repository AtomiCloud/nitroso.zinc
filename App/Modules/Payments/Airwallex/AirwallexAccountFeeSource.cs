using CSharp_Result;
using Domain.Payment;

namespace App.Modules.Payments.Airwallex;

// IGatewayAccountFeeSource over the Airwallex financial_transactions API:
// FEE-type transactions in a created-at window, mapped to account-fee lines.
// The API already filters by transaction_type; the defensive re-check keeps a
// lenient gateway from ever writing non-FEE rows as account fees.
public class AirwallexAccountFeeSource(AirWallexClient client) : IGatewayAccountFeeSource
{
  public const string FeeTransactionType = "FEE";

  public Task<Result<IEnumerable<GatewayAccountFeeLine>>> InRange(DateTime fromUtc, DateTime toUtc)
  {
    return client
      .ListFinancialTransactionsByType(FeeTransactionType, fromUtc, toUtc)
      .Then(
        items =>
          items
            .Where(x =>
              string.Equals(x.TransactionType, FeeTransactionType, StringComparison.OrdinalIgnoreCase)
            )
            .Select(x => new GatewayAccountFeeLine
            {
              SourceId = x.SourceId ?? string.Empty,
              FinancialTransactionId = x.Id,
              Amount = x.Amount,
              Net = x.Net,
              Currency = x.Currency,
              TransactedAt = DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc),
            }),
        Errors.MapNone
      );
  }
}
