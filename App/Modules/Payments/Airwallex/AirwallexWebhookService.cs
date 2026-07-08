using App.Error.Common;
using App.Error.V1;
using App.Utility;
using CSharp_Result;
using Domain.Payment;
using Domain.Withdrawal;

namespace App.Modules.Payments.Airwallex;

public class AirwallexWebhookService(
  IPaymentService paymentService,
  IWithdrawalService withdrawalService,
  AirwallexEventAdapter adapter,
  AirwallexHmacCalculator airwallexHmacCalculator,
  ILogger<AirwallexWebhookService> logger
)
{
  public Task<Result<Unit>> ProcessEvent(
    AirwallexEvent evt,
    string timestamp,
    string payload,
    string signature
  )
  {
    return airwallexHmacCalculator
      .Compute(timestamp, payload)
      .ToAsyncResult()
      .Then(x =>
        x == signature
          ? new Unit().ToResult()
          : new Unauthorized(
            "Incorrect Signature",
            [new Scope("x-signature", signature)],
            [new Scope("x-signature", x)]
          ).ToException()
      )
      .ThenAwait(_ =>
        AirwallexEventAdapter.IsTransferEvent(evt)
          ? this.ProcessTransfer(evt)
          : this.ProcessPayment(evt)
      );
  }

  // Deposits: payment_intent.* events resolve a Payment
  private Task<Result<Unit>> ProcessPayment(AirwallexEvent evt)
  {
    var (guid, record, complete) = adapter.ProcessEvent(evt);
    return complete
      ? paymentService.CompleteById(guid, record).Then(_ => new Unit(), Errors.MapNone)
      : paymentService.UpdateById(guid, record).Then(_ => new Unit(), Errors.MapNone);
  }

  // Payouts: transfer.* events resolve a Withdrawal
  private Task<Result<Unit>> ProcessTransfer(AirwallexEvent evt)
  {
    var (withdrawalId, transferId, outcome) = adapter.ProcessTransferEvent(evt);
    logger.LogInformation(
      "Airwallex transfer event '{Name}' for Withdrawal '{WithdrawalId}' (transfer '{TransferId}'): {Outcome}",
      evt.Name,
      withdrawalId,
      transferId,
      outcome
    );
    return outcome switch
    {
      TransferOutcome.Settled => withdrawalService
        .CompletePayout(withdrawalId, transferId)
        .Then(_ => new Unit(), Errors.MapNone),
      TransferOutcome.Failed => withdrawalService
        .FailPayout(withdrawalId, $"Airwallex transfer '{transferId}' ended as '{evt.Name}'")
        .Then(_ => new Unit(), Errors.MapNone),
      // NEW / PROCESSING / SENT etc: acknowledge, settlement decides later
      _ => Task.FromResult(new Unit().ToResult()),
    };
  }
}
