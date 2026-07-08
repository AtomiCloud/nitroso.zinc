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

  public int Limit { get; init; }

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

public record Withdrawal
{
  public required WithdrawalPrincipal Principal { get; init; }
  public required WalletPrincipal Wallet { get; init; }
  public required UserPrincipal User { get; init; }
  public required UserPrincipal? Completer { get; init; }
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

  public required string PayNowNumber { get; init; }
}
