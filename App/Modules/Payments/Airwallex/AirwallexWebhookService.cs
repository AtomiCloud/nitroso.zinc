using App.Error.Common;
using App.Error.V1;
using App.Utility;
using CSharp_Result;
using Domain.Exceptions;
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
  private async Task<Result<Unit>> ProcessTransfer(AirwallexEvent evt)
  {
    var (withdrawalId, transferId, attempt, outcome) = adapter.ProcessTransferEvent(evt);
    logger.LogInformation(
      "Airwallex transfer event '{Name}' for Withdrawal '{WithdrawalId}' (transfer '{TransferId}', attempt {Attempt}): {Outcome}",
      evt.Name,
      withdrawalId,
      transferId,
      attempt,
      outcome
    );

    // a transfer we did not mint (e.g. created by hand in the Airwallex
    // dashboard): acknowledge and ignore
    if (withdrawalId == null)
    {
      logger.LogWarning(
        "Ignoring Airwallex transfer event '{Name}' with foreign request id '{RequestId}' (transfer '{TransferId}')",
        evt.Name,
        evt.Data.Object.RequestId,
        transferId
      );
      return new Unit();
    }

    var result = await (
      outcome switch
      {
        TransferOutcome.Settled => withdrawalService
          .CompletePayout(withdrawalId.Value, transferId, attempt)
          .Then(_ => new Unit(), Errors.MapNone),
        TransferOutcome.Failed => withdrawalService
          .FailPayout(
            withdrawalId.Value,
            $"Airwallex transfer '{transferId}' ended as '{evt.Name}'",
            attempt
          )
          .Then(_ => new Unit(), Errors.MapNone),
        // NEW / PROCESSING / SENT etc: acknowledge, settlement decides later
        _ => Task.FromResult(new Unit().ToResult()),
      }
    );

    // stale events (superseded attempt, already-terminal withdrawal) must be
    // acknowledged with 2xx or the gateway redelivers them forever
    if (result.IsFailure() && result.FailureOrDefault() is StalePayoutEventException stale)
    {
      logger.LogWarning(
        "Ignoring stale Airwallex transfer event '{Name}' for Withdrawal '{WithdrawalId}': {Reason}",
        evt.Name,
        withdrawalId,
        stale.Message
      );
      return new Unit();
    }
    return result;
  }
}
