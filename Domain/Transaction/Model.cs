using Domain.Payment;
using Domain.Wallet;

namespace Domain.Transaction;

public static class TransactionTypes
{
  public const string BookingRequest = "BookingRequest";
  public const string BookingComplete = "BookingComplete";
  public const string BookingRefund = "BookingRefund";
  public const string BookingCancel = "BookingCancel";
  public const string BookingTerminated = "BookingTerminated";

  // Wallet Related
  public const string Deposit = "Deposit";
  public const string WithdrawRequest = "WithdrawRequest";
  public const string WithdrawComplete = "WithdrawComplete";
  public const string WithdrawRejected = "WithdrawRejected";
  public const string WithdrawCancelled = "WithdrawCancelled";

  // Additional
  public const string Promotional = "Promotional";
  public const string Transfer = "Transfer";

  public static readonly string[] Values =
  [
    BookingRequest,
    BookingCancel,
    BookingComplete,
    BookingRefund,
    BookingTerminated,
    Deposit,
    WithdrawComplete,
    WithdrawRequest,
    WithdrawRejected,
    WithdrawCancelled,
    Promotional,
    Transfer,
  ];
}

public enum TransactionType
{
  // Product related
  BookingRequest = 0,
  BookingComplete = 1,
  BookingRefund = 2,
  BookingCancel = 3,
  BookingTerminated = 4,

  // Wallet Related
  Deposit = 5,
  WithdrawRequest = 6,
  WithdrawComplete = 7,
  WithdrawRejected = 8,
  WithdrawCancelled = 9,

  // Additional
  Promotional = 10,
  Transfer = 11,
}

public record TransactionSearch
{
  public string? Search;

  public TransactionType? TransactionType;
  public Guid? Id;
  public string? userId;
  public Guid? WalletId;
  public DateOnly? Before;
  public DateOnly? After;
  public string? Reference;

  public int Limit { get; init; }
  public int Skip { get; init; }
}

public record Transaction
{
  public required TransactionPrincipal Principal { get; init; }

  public required PaymentPrincipal? Payment { get; init; }

  public required WalletPrincipal Wallet { get; init; }
}

public record TransactionPrincipal
{
  public required Guid Id { get; init; }
  public required DateTime CreatedAt { get; init; }
  public required TransactionRecord Record { get; init; }
}

public record TransactionRecord
{
  public required string Name { get; init; }
  public required string Description { get; init; }
  public required TransactionType Type { get; init; }
  public required decimal Amount { get; init; }

  public required string From { get; init; }
  public required string To { get; init; }
}
