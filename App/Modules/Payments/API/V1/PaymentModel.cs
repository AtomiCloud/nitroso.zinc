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

// intent-level evidence rows for the analysis page: which payment intents
// captured money in the (inclusive SGT date, dd-MM-yyyy) range
public record CapturedPaymentsQueryReq(string? After, string? Before, int? Limit);

// gateway-fee sync range (inclusive SGT dates, dd-MM-yyyy; null = unbounded)
public record GatewayFeeSyncQueryReq(string? After, string? Before);

// RESP

// gateway-fee sync outcome: Synced = sources that gained fee rows this call;
// Missing = sources the gateway has no fee rows for yet (fees post with
// delay — sync again later); HasMore = the per-call bound was hit, call again
public record GatewayFeeSyncRes(int Synced, string[] Missing, bool HasMore);
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

// one captured intent (newest first); PaymentIntentId = the gateway's
// external reference (the Airwallex intent id)
public record CapturedPaymentRes(
  string PaymentIntentId,
  decimal CapturedAmount,
  string Currency,
  DateTime CreatedAt,
  string Status
);
