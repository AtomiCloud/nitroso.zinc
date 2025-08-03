using App.Modules.Common;
using App.Modules.Wallets.API.V1;
using Domain.User;

namespace App.Modules.Users.API.V1;

public static class UserMapper
{
  // RES
  public static UserPrincipalRes ToRes(this UserPrincipal userPrincipal) =>
    new(userPrincipal.Id, userPrincipal.Record.Username, userPrincipal.Record.Email, 
      userPrincipal.Record.EmailVerified, userPrincipal.Record.Roles);

  public static UserRes ToRes(this User user) => new(user.Principal.ToRes(), user.Wallet.ToRes());

  // REQ
  public static UserRecord ToRecord(this CreateUserReq req, UserToken token) =>
    new()
    {
      Username = req.Username,
      Email = token.Email,
      EmailVerified = token.EmailVerified,
      Roles = token.Roles,
    };

  public static UserRecord ToRecord(this UpdateUserReq req, UserToken token) =>
    new()
    {
      Username = req.Username,
      Email = token.Email,
      EmailVerified = token.EmailVerified,
      Roles = token.Roles,
    };

  public static UserSearch ToDomain(this SearchUserQuery query) =>
    new()
    {
      Id = query.Id,
      Username = query.Username,
      Email = query.Email,
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };
}
