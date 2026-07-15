using App.Modules.Wallets.API.V1;

namespace App.Modules.Users.API.V1;

public record SearchUserQuery(string? Id, string? Username, string? Email, int? Limit, int? Skip);

// REQ
public record CreateUserReq(string Username, string? IdToken, string? AccessToken);

public record UpdateUserReq(string Username, string? IdToken, string? AccessToken);

// inclusive SGT source-event dates (dd-MM-yyyy); null = unbounded
public record PartnerPnlQueryReq(string? After, string? Before);

// RESP
public record UserExistRes(bool Exists);

public record PartnerUserRes(string Id, string Username, string Email);

// Bookings is the full ticket count; BoostCount/BoostAmount cover the subset
// with a consumed priority boost (appended fields — additive wire change)
public record PartnerPnlRowRes(
  string Month,
  int Bookings,
  decimal Collected,
  decimal KtmbCost,
  decimal Deposits,
  decimal WithdrawalGross,
  decimal WithdrawalFeeIncome,
  int BoostCount,
  decimal BoostAmount
);

// ExtraRoles: admin-managed roles for discount/pricing targeting — distinct
// from Roles (which mirror the Descope JWT and are overwritten by the token
// sync); never used for authorization
public record UserPrincipalRes(
  string Id,
  string Username,
  string? Email,
  bool? EmailVerified,
  string[]? Roles,
  string[] ExtraRoles
);

public record UserRes(UserPrincipalRes Principal, WalletPrincipalRes Wallet);
