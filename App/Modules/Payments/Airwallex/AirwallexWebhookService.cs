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
        AirwallexEventAdapter.IsTransferEvent(evt) ? this.ProcessTransfer(evt)
        : AirwallexEventAdapter.IsRefundEvent(evt) ? this.ProcessRefund(evt)
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

  // Card refunds: refund.* events resolve a fragment of a card-refund
  // Withdrawal (settled fragments may complete the withdrawal; a failed
  // fragment parks it for a human)
  private async Task<Result<Unit>> ProcessRefund(AirwallexEvent evt)
  {
    var (withdrawalId, requestId, refundId, attempt, outcome, arn) = adapter.ProcessRefundEvent(
      evt
    );
    logger.LogInformation(
      "Airwallex refund event '{Name}' for Withdrawal '{WithdrawalId}' (refund '{RefundId}', request '{RequestId}', attempt {Attempt}): {Outcome}",
      evt.Name,
      withdrawalId,
      refundId,
      requestId,
      attempt,
      outcome
    );

    // a refund we did not mint (e.g. issued by hand on the Airwallex
    // dashboard): acknowledge and ignore
    if (withdrawalId == null)
    {
      logger.LogWarning(
        "Ignoring Airwallex refund event '{Name}' with foreign request id '{RequestId}' (refund '{RefundId}')",
        evt.Name,
        requestId,
        refundId
      );
      return new Unit();
    }

    var result = await (
      outcome switch
      {
        TransferOutcome.Settled => withdrawalService
          .SettleRefundFragment(withdrawalId.Value, requestId, refundId, attempt, arn)
          .Then(_ => new Unit(), Errors.MapNone),
        TransferOutcome.Failed => withdrawalService
          .FailRefundFragment(
            withdrawalId.Value,
            requestId,
            refundId,
            $"Airwallex refund '{refundId}' ended as '{evt.Name}'",
            attempt
          )
          .Then(_ => new Unit(), Errors.MapNone),
        // RECEIVED / ACCEPTED: record the progress for the admin page;
        // settlement decides later
        _ => RecordProgress(withdrawalId.Value, requestId, refundId, RefundEventStatus(evt), arn),
      }
    );

    if (result.IsFailure() && result.FailureOrDefault() is StalePayoutEventException stale)
    {
      logger.LogWarning(
        "Ignoring stale Airwallex refund event '{Name}' for Withdrawal '{WithdrawalId}': {Reason}",
        evt.Name,
        withdrawalId,
        stale.Message
      );
      return new Unit();
    }
    return result;
  }

  // Display-only bookkeeping for an in-flight refund event. A failure here is
  // logged and acknowledged, never surfaced: a non-2xx would make Airwallex
  // redeliver an event that moves no money, and the next reconcile records
  // the same progress anyway.
  private async Task<Result<Unit>> RecordProgress(
    Guid withdrawalId,
    string requestId,
    string refundId,
    string status,
    string? arn
  )
  {
    Result<Unit> recorded;
    try
    {
      recorded = await withdrawalService.RecordRefundProgress(requestId, refundId, status, arn);
    }
    catch (Exception e)
    {
      // some repository reads rethrow rather than return their failure
      recorded = e;
    }
    if (recorded.IsFailure())
      logger.LogWarning(
        recorded.FailureOrDefault(),
        "Could not record Airwallex refund progress '{Status}' for Withdrawal '{WithdrawalId}' (refund '{RefundId}'); acknowledging anyway",
        status,
        withdrawalId,
        refundId
      );
    return new Unit();
  }

  // the refund's new status as the event object carries it; falls back to
  // the event name's last segment ("refund.accepted" -> "ACCEPTED")
  private static string RefundEventStatus(AirwallexEvent evt)
  {
    var status = evt.Data.Object.Status;
    if (!string.IsNullOrWhiteSpace(status))
      return status.ToUpperInvariant();
    var dot = evt.Name.LastIndexOf('.');
    return (dot >= 0 ? evt.Name[(dot + 1)..] : evt.Name).ToUpperInvariant();
  }
}
