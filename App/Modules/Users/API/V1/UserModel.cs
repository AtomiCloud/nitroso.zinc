using App.Modules.Wallets.API.V1;

namespace App.Modules.Users.API.V1;

public record SearchUserQuery(string? Id, string? Username, string? Email, int? Limit, int? Skip);

// REQ
public record CreateUserReq(string Username, string? IdToken, string? AccessToken);

public record UpdateUserReq(string Username, string? IdToken, string? AccessToken);

// RESP
public record UserExistRes(bool Exists);

public record UserPrincipalRes(string Id, string Username, string? Email, bool? EmailVerified, string[]? Roles);

public record UserRes(UserPrincipalRes Principal, WalletPrincipalRes Wallet);
