using App.Modules.Wallets.API.V1;

namespace App.Modules.Users.API.V1;

public record SearchUserQuery(string? Id, string? Username, string? Email, int? Limit, int? Skip);

// REQ
public record CreateUserReq(string Username, string? IdToken, string? AccessToken);

public record UpdateUserReq(string Username, string? IdToken, string? AccessToken);

// RESP
public record UserExistRes(bool Exists);

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
