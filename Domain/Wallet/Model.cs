using Domain.User;

namespace Domain.Wallet;

public record WalletSearch
{
  public Guid? Id { get; init; }

  public string? UserId { get; init; }

  public int Limit { get; init; }

  public int Skip { get; init; }
}

public record Wallet
{
  public required WalletPrincipal Principal { get; init; }

  public required UserPrincipal User { get; init; }
}

public record WalletPrincipal
{
  public required Guid Id { get; init; }
  public required WalletRecord Record { get; init; }
}

public record WalletRecord
{
  public required decimal Usable { get; init; }
  public required decimal WithdrawReserve { get; init; }
  public required decimal BookingReserve { get; init; }
}
