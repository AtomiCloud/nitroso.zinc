using System.Text.Json;
using Domain.Payment;

namespace App.Modules.Payments.Airwallex;

public enum TransferOutcome
{
  // non-terminal statuses (e.g. NEW, PROCESSING, SENT): acknowledge and wait
  InFlight = 0,
  Settled = 1,
  Failed = 2,
}

public class AirwallexEventAdapter
{
  private static readonly string[] SettledStatuses = AirwallexTransferStatuses.Settled;

  private static readonly string[] FailedStatuses = AirwallexTransferStatuses.Failed;

  public (Guid, PaymentRecord, bool) ProcessEvent(AirwallexEvent evt)
  {
    var id = Guid.Parse(evt.Data.Object.RequestId);
    var record = new PaymentRecord
    {
      Amount = evt.Data.Object.Amount,
      CapturedAmount = evt.Data.Object.CapturedAmount,
      Currency = evt.Data.Object.Currency,
      LastUpdated = DateTime.UtcNow,
      Status = evt.Data.Object.Status,
      AdditionalData = JsonDocument.Parse("{}"),
    };
    var complete = evt.Data.Object.Status == "SUCCEEDED";
    return (id, record, complete);
  }

  // True for payout (transfer.*) events, which resolve a Withdrawal instead
  // of a Payment
  public static bool IsTransferEvent(AirwallexEvent evt) =>
    evt.Name.StartsWith("transfer", StringComparison.OrdinalIgnoreCase);

  // True for refund.* events (refund.received/accepted/settled/failed),
  // which resolve a card-refund Withdrawal fragment
  public static bool IsRefundEvent(AirwallexEvent evt) =>
    evt.Name.StartsWith("refund.", StringComparison.OrdinalIgnoreCase);

  // Refund request ids are "{withdrawalId}-{attempt}-{index}". WithdrawalId
  // is null for ids not minted by us (e.g. a refund issued by hand on the
  // Airwallex dashboard) — such events must be acknowledged and ignored.
  public (
    Guid? WithdrawalId,
    string RequestId,
    string RefundId,
    int? Attempt,
    TransferOutcome Outcome
  ) ProcessRefundEvent(AirwallexEvent evt)
  {
    var requestId = evt.Data.Object.RequestId;
    Guid? withdrawalId = null;
    int? attempt = null;
    var parts = requestId.Split('-');
    // guid is 5 dash-separated groups; ours is guid + attempt + index = 7
    if (parts.Length == 7 && Guid.TryParse(string.Join('-', parts[..5]), out var parsed))
    {
      withdrawalId = parsed;
      if (int.TryParse(parts[5], out var n))
        attempt = n;
    }

    var status = evt.Data.Object.Status.ToUpperInvariant();
    var outcome = AirwallexRefundStatuses.Settled.Contains(status) ? TransferOutcome.Settled
      : AirwallexRefundStatuses.Failed.Contains(status) ? TransferOutcome.Failed
      : TransferOutcome.InFlight;

    return (withdrawalId, requestId, evt.Data.Object.Id, attempt, outcome);
  }

  // Transfer request ids are "{withdrawalId}-{attempt}"; the number after the
  // last dash is the attempt counter, everything before it the withdrawal id.
  // WithdrawalId is null for ids not minted by us (e.g. a transfer created by
  // hand in the Airwallex dashboard) — such events must be acknowledged and
  // ignored, never crash the webhook into a redelivery loop. Attempt is null
  // for a bare-guid id — the service then skips the attempt fence.
  public (
    Guid? WithdrawalId,
    string TransferId,
    int? Attempt,
    TransferOutcome Outcome
  ) ProcessTransferEvent(AirwallexEvent evt)
  {
    var requestId = evt.Data.Object.RequestId;
    var cut = requestId.LastIndexOf('-');
    Guid? withdrawalId = null;
    int? attempt = null;
    if (Guid.TryParse(requestId, out var bare))
    {
      withdrawalId = bare;
    }
    else if (cut > 0 && Guid.TryParse(requestId[..cut], out var prefixed))
    {
      withdrawalId = prefixed;
      if (int.TryParse(requestId[(cut + 1)..], out var n))
        attempt = n;
    }

    var status = evt.Data.Object.Status.ToUpperInvariant();
    var outcome = SettledStatuses.Contains(status) ? TransferOutcome.Settled
      : FailedStatuses.Contains(status) ? TransferOutcome.Failed
      : TransferOutcome.InFlight;

    return (withdrawalId, evt.Data.Object.Id, attempt, outcome);
  }
}
