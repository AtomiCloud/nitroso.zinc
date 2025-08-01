using System.Text.Json;
using App.Modules.Transactions.API.V1;
using App.Modules.Users.API.V1;
using App.Modules.Wallets.API.V1;

namespace App.Modules.Payments.API.V1;

// REQ
public record SearchPaymentQuery(
  Guid? Id,
  Guid? WalletId,
  Guid? TransactionId,
  string? Reference,
  string? Gateway,
  decimal? Min,
  decimal? Max,
  // date only
  string? CreatedBefore,
  string? CreatedAfter,
  string? LastUpdatedBefore,
  string? LastUpdatedAfter,
  string? Status,
  int? Limit,
  int? Skip
);

public record CreatePaymentReq(decimal Amount, string Currency);

// RESP
public record CreatePaymentRes(
  Guid Id,
  string ExternalReference,
  string Gateway,
  string Secret,
  DateTime CreatedAt,
  Dictionary<string, DateTime> Statuses,
  decimal Amount,
  string Currency,
  string Status,
  DateTime LastUpdated,
  JsonDocument AdditionalData
);

public record PaymentPrincipalRes(
  Guid Id,
  string ExternalReference,
  string Gateway,
  DateTime CreatedAt,
  Dictionary<string, DateTime> Statuses,
  decimal Amount,
  decimal CapturedAmount,
  string Currency,
  string Status,
  DateTime LastUpdated,
  JsonDocument AdditionalData
);

public record PaymentRes(
  PaymentPrincipalRes Principal,
  WalletPrincipalRes Wallet,
  TransactionPrincipalRes? Transaction
);
