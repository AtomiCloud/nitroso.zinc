using App.Modules.Payments.Airwallex;
using CSharp_Result;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.Data;

public class AirwallexPayoutGateway(AirWallexClient client) : IPayoutGateway
{
  private const string Currency = "SGD";

  public Task<Result<PayoutConfirmation>> CreatePayout(PayoutRequest request)
  {
    var req = new AirwallexCreateTransferReq
    {
      RequestId = request.RequestId,
      SourceCurrency = Currency,
      TransferCurrency = Currency,
      TransferAmount = request.Amount,
      TransferMethod = "LOCAL",
      Reason = "personal_funds_transfer",
      Reference = "BunnyBooker Withdrawal",
      Beneficiary = new AirwallexTransferBeneficiary
      {
        EntityType = "PERSONAL",
        BankDetails = new AirwallexTransferBankDetails
        {
          AccountCurrency = Currency,
          BankCountryCode = "SG",
          AccountRoutingType1 = "paynow_id",
          AccountRoutingValue1 = ToPayNowId(request.PayNowNumber),
        },
      },
    };
    return client
      .CreateTransfer(req)
      .Then(res => new PayoutConfirmation { Id = res.Id }, Errors.MapNone);
  }

  public Task<Result<PayoutStatus>> GetPayoutStatus(string requestId, string? confirmationNumber)
  {
    var lookup =
      confirmationNumber != null
        ? client.GetTransfer(confirmationNumber)
        : client.GetTransferByRequestId(requestId);
    return lookup.Then(
      transfer =>
      {
        if (transfer == null)
          return new PayoutStatus { Outcome = PayoutOutcome.NotFound, ConfirmationNumber = null };
        var status = transfer.Status.ToUpperInvariant();
        var outcome = AirwallexTransferStatuses.Settled.Contains(status) ? PayoutOutcome.Settled
          : AirwallexTransferStatuses.Failed.Contains(status) ? PayoutOutcome.Failed
          : PayoutOutcome.InFlight;
        return new PayoutStatus { Outcome = outcome, ConfirmationNumber = transfer.Id };
      },
      Errors.MapNone
    );
  }

  // PayNow mobile IDs are addressed in E.164 form; local 8-digit numbers get
  // the Singapore country prefix
  private static string ToPayNowId(string payNowNumber) =>
    payNowNumber.StartsWith('+') ? payNowNumber : $"+65{payNowNumber}";
}
