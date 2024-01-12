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
}

public record WithdrawalStatus
{
  public required WithdrawStatus Status { get; init; }
}

public record WithdrawalComplete
{
  public required DateTime CompletedAt { get; init; }

  public required string CompleterId { get; init; }

  public required string Note { get; init; }

  public required string? Receipt { get; init; }
}

public record WithdrawalRecord
{
  public required decimal Amount { get; init; }

  public required string PayNowNumber { get; init; }
}
