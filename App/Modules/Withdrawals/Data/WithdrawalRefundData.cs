using System.ComponentModel.DataAnnotations;
using App.Modules.Payments.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Withdrawals.Data;

// One card refund created (or planned) against a specific funding payment —
// the user-visible evidence of where a card withdrawal's money went, and the
// per-intent refunded-total ledger the refundable pool subtracts.
public class WithdrawalRefundData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // fragment lifecycle: 0 Created, 1 Settled, 2 Failed
  public byte Status { get; set; }

  [Precision(16, 8)]
  public decimal Amount { get; set; }

  // denormalized Airwallex payment intent id (PaymentData.ExternalReference)
  [MaxLength(256)]
  public string PaymentIntentId { get; set; } = string.Empty;

  // gateway refund id, once the create call returned
  [MaxLength(64)]
  public string? AirwallexRefundId { get; set; }

  // Acquirer Reference Number, captured once the refund settles. Nullable
  // with NO empty-string default on purpose: "not settled yet" and "the
  // network issued none" must both read as absent, and a "" would make an
  // unbackfilled row indistinguishable from a captured one. ARNs are 23
  // digits; 64 matches the refund id next to it.
  [MaxLength(64)]
  public string? AcquirerReferenceNumber { get; set; }

  // deterministic gateway idempotency key: "{withdrawalId}-{attempt}-{index}"
  [MaxLength(128)]
  public string RequestId { get; set; } = string.Empty;

  public DateTime? SettledAt { get; set; }

  // References
  public Guid WithdrawalId { get; set; }
  public WithdrawalData Withdrawal { get; set; } = null!;

  public Guid PaymentId { get; set; }
  public PaymentData Payment { get; set; } = null!;
}
