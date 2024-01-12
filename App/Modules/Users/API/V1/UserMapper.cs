using App.Modules.Wallets.API.V1;
using Domain.User;

namespace App.Modules.Users.API.V1;

public static class UserMapper
{
  // RES
  public static UserPrincipalRes ToRes(this UserPrincipal userPrincipal)
    => new(userPrincipal.Id, userPrincipal.Record.Username);

  public static UserRes ToRes(this User user)
    => new(user.Principal.ToRes(), user.Wallet.ToRes());


  // REQ
  public static UserRecord ToRecord(this CreateUserReq req) =>
    new() { Username = req.Username };

  public static UserRecord ToRecord(this UpdateUserReq req) =>
    new() { Username = req.Username };

  public static UserSearch ToDomain(this SearchUserQuery query) =>
    new()
    {
      Id = query.Id,
      Username = query.Username,
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };
}
