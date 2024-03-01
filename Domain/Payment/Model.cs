using System.Text.Json;
using Domain.Transaction;
using Domain.Wallet;

namespace Domain.Payment;

public record PaymentSearch
{
  public Guid? Id;
  public Guid? WalletId;
  public Guid? TransactionId;

  public string? Reference;
  public string? Gateway;
  public decimal? MinAmount;
  public decimal? MaxAmount;

  public DateTime? CreatedBefore;
  public DateTime? CreatedAfter;
  public DateTime? LastUpdatedBefore;
  public DateTime? LastUpdatedAfter;

  public string? Status;

  public int Limit { get; init; }
  public int Skip { get; init; }
}

public record PaymentSecret
{
  public required string Secret { get; init; }
}

public record Payment
{
  public required TransactionPrincipal? Transaction { get; init; }

  public required PaymentPrincipal Principal { get; init; }

  public required WalletPrincipal Wallet { get; init; }
}

public record PaymentReference
{
  public required Guid Id { get; init; }

  public required string ExternalReference { get; init; }

  public required string Gateway { get; init; }
}

public record PaymentPrincipal
{
  public required PaymentReference Reference { get; init; }
  public required PaymentRecord Record { get; init; }
  public required DateTime CreatedAt { get; init; }

  public required Dictionary<string, DateTime> Statuses { get; init; }
}

public record PaymentRecord
{
  public required decimal Amount { get; init; }
  public required decimal CapturedAmount { get; init; }

  public required string Currency { get; init; }

  public required DateTime LastUpdated { get; init; }

  public required string Status { get; init; }


  public required JsonDocument AdditionalData { get; init; }
}
