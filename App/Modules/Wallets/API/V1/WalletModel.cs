using App.Modules.Users.API.V1;

namespace App.Modules.Wallets.API.V1;

public record SearchWalletQuery(
  string? UserId,
  Guid? Id,
  int? Limit,
  int? Skip
);

// RESP
public record WalletPrincipalRes(Guid Id, string UserId, decimal Usable, decimal WithdrawReserve, decimal BookingReserve);

public record WalletRes(WalletPrincipalRes Principal, UserPrincipalRes User);
