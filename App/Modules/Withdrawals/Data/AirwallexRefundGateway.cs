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

  public Task<Result<PayoutStatus>> GetRefundStatus(string refundId)
  {
    return client
      .GetRefund(refundId)
      .Then(
        refund =>
        {
          if (refund == null)
            return new PayoutStatus { Outcome = PayoutOutcome.NotFound, ConfirmationNumber = null };
          var status = refund.Status.ToUpperInvariant();
          var outcome = AirwallexRefundStatuses.Settled.Contains(status) ? PayoutOutcome.Settled
            : AirwallexRefundStatuses.Failed.Contains(status) ? PayoutOutcome.Failed
            : PayoutOutcome.InFlight;
          return new PayoutStatus { Outcome = outcome, ConfirmationNumber = refund.Id };
        },
        Errors.MapNone
      );
  }
}
