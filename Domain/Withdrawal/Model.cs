using Domain.User;
using Domain.Wallet;

namespace Domain.Withdrawal;

public record WithdrawalSearch
{
  public Guid? Id { get; init; }

  public string? UserId { get; init; }

  public string? CompleterId { get; init; }

  public decimal? Min { get; init; }
  public decimal? Max { get; init; }

  public WithdrawStatus? Status { get; init; }

  public DateOnly? Before { get; init; }

  public DateOnly? After { get; init; }

  public int Limit { get; init; } = 20;

  public int Skip { get; init; }
}

public enum WithdrawStatus
{
  Pending = 0,
  Completed = 1,
  Rejected = 2,
  Cancel = 3,

  // payout initiated at the gateway, awaiting settlement confirmation via
  // webhook (appended to preserve stored byte values)
  Processing = 4,

  // parking state: the payout sat in Processing through the maximum number
  // of reconciliation attempts without the gateway ever confirming or
  // denying it — a human must check the Airwallex dashboard and resolve via
  // force-complete (money left), reject (refund) or requeue (retry)
  RequireManualIntervention = 5,
}

// How the money leaves BunnyBooker: a PayNow transfer to the user's mobile
// number, or refunds against the card payments that funded the wallet
public enum WithdrawalMethod
{
  PayNow = 0,
  CardRefund = 1,
}

public record Withdrawal
{
  public required WithdrawalPrincipal Principal { get; init; }
  public required WalletPrincipal Wallet { get; init; }
  public required UserPrincipal User { get; init; }
  public required UserPrincipal? Completer { get; init; }

  // Card-refund evidence: one fragment per refund created at the gateway.
  // Empty for PayNow withdrawals (defaulted so pre-card-refund call sites and
  // fakes need no change)
  public IReadOnlyList<WithdrawalRefundFragment> Refunds { get; init; } = [];
}

public record WithdrawalPrincipal
{
  public required Guid Id { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required WithdrawalStatus Status { get; init; }

  public required WithdrawalRecord Record { get; init; }

  public required WithdrawalComplete? Complete { get; init; }

  public required WithdrawalPayout? Payout { get; init; }
}

public record WithdrawalStatus
{
  public required WithdrawStatus Status { get; init; }
}

public record WithdrawalComplete
{
  public required DateTime CompletedAt { get; init; }

  // null when the withdrawal was completed by the automated payout flow
  // (no human completer exists)
  public required string? CompleterId { get; init; }

  public required string Note { get; init; }

  public required string? Receipt { get; init; }
}

// Automated-payout bookkeeping, written when an approve attempt fires.
// ConfirmationNumber is the gateway transfer id proving the payout exists.
public record WithdrawalPayout
{
  public required string? ConfirmationNumber { get; init; }

  // Fee snapshotted at approval so the ledger always matches the transfer
  // actually created, even if the configured rate changes mid-flight
  public required decimal Fee { get; init; }

  // Number of approve attempts; makes each gateway request id unique so a
  // genuinely failed payout can be retried without colliding with the
  // gateway's idempotency window
  public required int Attempt { get; init; }

  // Number of inconclusive reconciliation sweeps while stuck in Processing;
  // at MaxReconcileAttempts the withdrawal is parked for a human
  public int ReconcileAttempts { get; init; }
}

public record WithdrawalRecord
{
  public required decimal Amount { get; init; }

  public required WithdrawalMethod Method { get; init; }

  // required for PayNow (validated at the API); null for CardRefund — the
  // money returns to the cards that funded the wallet, no PayNow id exists
  public required string? PayNowNumber { get; init; }
}

// Lifecycle of a single refund fragment at the gateway
public enum RefundFragmentStatus
{
  // created at the gateway (or planned), awaiting settlement
  Created = 0,
  Settled = 1,
  Failed = 2,
}

// One card refund created (or planned) against a specific funding payment.
// A card withdrawal's net amount is covered by one or more fragments, walked
// oldest-payment-first; the rows double as the user-visible EVIDENCE of where
// the money went and as the per-intent refunded-total ledger.
public record WithdrawalRefundFragment
{
  public required Guid Id { get; init; }

  public required Guid WithdrawalId { get; init; }

  // the PaymentData row that funded this fragment
  public required Guid PaymentId { get; init; }

  // denormalized Airwallex payment intent id (PaymentData.ExternalReference)
  public required string PaymentIntentId { get; init; }

  // gateway refund id, once the create call returned
  public required string? AirwallexRefundId { get; init; }

  // Acquirer Reference Number: the card network's handle on the refund, and
  // what a tax auditor or the user's issuing bank traces the money by. The
  // gateway publishes it only after the refund SETTLES, so null means "not
  // captured yet" — never "this refund has none". Defaulted so pre-ARN call
  // sites and fakes need no change.
  public string? AcquirerReferenceNumber { get; init; }

  // deterministic gateway idempotency key: "{withdrawalId}-{attempt}-{index}"
  public required string RequestId { get; init; }

  public required decimal Amount { get; init; }

  public required RefundFragmentStatus Status { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required DateTime? SettledAt { get; init; }
}

// A captured card payment that funded the wallet — the raw input to the
// refundable-pool computation
public record FundingPayment
{
  public required Guid PaymentId { get; init; }

  public required string PaymentIntentId { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required decimal CapturedAmount { get; init; }
}

// A funding payment still inside the refund window, and how much of its
// captured amount remains refundable (captured minus non-failed fragments)
public record RefundablePayment
{
  public required Guid PaymentId { get; init; }

  public required string PaymentIntentId { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required decimal Refundable { get; init; }
}
