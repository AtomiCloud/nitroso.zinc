using App.Modules.Users.API.V1;
using App.Modules.Wallets.API.V1;

namespace App.Modules.Withdrawals.API.V1;

public record SearchWithdrawalQuery(
  Guid? Id,
  string? UserId,
  string? CompleterId,
  decimal? Min,
  decimal? Max,
  string? Status,
  string? Before,
  string? After,
  int? Limit,
  int? Skip
);

public record CreateWithdrawalReq(decimal Amount, string PayNowNumber);

public record CancelWithdrawalReq(string Note);

public record RejectWithdrawalReq(string Note);

// RESP
public record WithdrawalStatusRes(string Status);

public record WithdrawalCompleteRes(DateTime CompletedAt, string Note, string? Receipt);

public record WithdrawalRecordRes(decimal Amount, string PayNowNumber);

public record WithdrawalPrincipalRes(
  Guid Id,
  DateTime CreateAt,
  WithdrawalStatusRes Status,
  WithdrawalRecordRes Record,
  WithdrawalCompleteRes? Complete
);

public record WithdrawalRes(
  WithdrawalPrincipalRes Principal,
  UserPrincipalRes User,
  UserPrincipalRes? Completer,
  WalletPrincipalRes Wallet
);
