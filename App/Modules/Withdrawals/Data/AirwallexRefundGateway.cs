using App.Modules.Payments.Airwallex;
using CSharp_Result;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.Data;

// Card-refund rail: returns withdrawal money to the cards behind the
// payment intents that funded the wallet, via the Airwallex Refunds API
public class AirwallexRefundGateway(AirWallexClient client) : IRefundGateway
{
  public Task<Result<RefundConfirmation>> CreateRefund(RefundRequest request)
  {
    var req = new AirwallexCreateRefundReq
    {
      RequestId = request.RequestId,
      PaymentIntentId = request.PaymentIntentId,
      Amount = request.Amount,
      Reason = "BunnyBooker Withdrawal",
    };
    return client
      .CreateRefund(req)
      .Then(res => new RefundConfirmation { Id = res.Id }, Errors.MapNone);
  }

  public Task<Result<List<GatewayRefund>>> ListRefunds(DateTime fromUtc, DateTime toUtc)
  {
    return client
      .ListRefunds(fromUtc, toUtc)
      .Then(
        refunds =>
          refunds
            .Select(r => new GatewayRefund
            {
              Id = r.Id,
              PaymentIntentId = r.PaymentIntentId,
              Amount = r.Amount,
              Outcome = Classify(r.Status),
              // a blank ARN is the same fact as an absent one
              AcquirerReferenceNumber = string.IsNullOrWhiteSpace(r.AcquirerReferenceNumber)
                ? null
                : r.AcquirerReferenceNumber,
              CreatedAt = r.CreatedAt,
              UpdatedAt = r.UpdatedAt,
              RequestId = string.IsNullOrWhiteSpace(r.RequestId) ? null : r.RequestId,
            })
            .ToList(),
        Errors.MapNone
      );
  }

  private static PayoutOutcome Classify(string status)
  {
    var normalized = status.ToUpperInvariant();
    return AirwallexRefundStatuses.Settled.Contains(normalized) ? PayoutOutcome.Settled
      : AirwallexRefundStatuses.Failed.Contains(normalized) ? PayoutOutcome.Failed
      : PayoutOutcome.InFlight;
  }

  public Task<Result<RefundStatus>> GetRefundStatus(string refundId)
  {
    return client
      .GetRefund(refundId)
      .Then(
        refund =>
        {
          if (refund == null)
            return new RefundStatus
            {
              Outcome = PayoutOutcome.NotFound,
              ConfirmationNumber = null,
              AcquirerReferenceNumber = null,
            };
          return new RefundStatus
          {
            Outcome = Classify(refund.Status),
            ConfirmationNumber = refund.Id,
            // a blank ARN is the same fact as an absent one: the network has
            // not issued a reference, and only null leaves a stored value alone
            AcquirerReferenceNumber = string.IsNullOrWhiteSpace(refund.AcquirerReferenceNumber)
              ? null
              : refund.AcquirerReferenceNumber,
          };
        },
        Errors.MapNone
      );
  }
}
