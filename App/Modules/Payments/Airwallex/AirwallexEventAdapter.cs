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
  private static readonly string[] SettledStatuses = ["PAID", "SETTLED"];

  private static readonly string[] FailedStatuses =
  [
    "FAILED",
    "CANCELLED",
    "REJECTED",
    "RETURNED",
  ];

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

  // Transfer request ids are "{withdrawalId}-{attempt}"; the number after the
  // last dash is the attempt counter, everything before it the withdrawal id.
  // Attempt is null for ids not minted by us (e.g. a bare guid) — the service
  // then skips the attempt fence.
  public (
    Guid WithdrawalId,
    string TransferId,
    int? Attempt,
    TransferOutcome Outcome
  ) ProcessTransferEvent(AirwallexEvent evt)
  {
    var requestId = evt.Data.Object.RequestId;
    var cut = requestId.LastIndexOf('-');
    Guid withdrawalId;
    int? attempt = null;
    if (Guid.TryParse(requestId, out var bare))
    {
      withdrawalId = bare;
    }
    else
    {
      withdrawalId = Guid.Parse(requestId[..cut]);
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
