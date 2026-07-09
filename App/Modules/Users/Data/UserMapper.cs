using App.Modules.Wallets.Data;
using Domain.User;

namespace App.Modules.Users.Data;

public static class UserMapper
{
  public static UserRecord ToRecord(this UserData principal) =>
    new()
    {
      Username = principal.Username,
      Email = principal.Email,
      EmailVerified = principal.EmailVerified,
      Roles = principal.Roles,
      ExtraRoles = principal.ExtraRoles,
    };

  public static UserPrincipal ToPrincipal(this UserData data) =>
    new() { Id = data.Id, Record = data.ToRecord() };

  public static User ToDomain(this UserData data) =>
    new()
    {
      Principal = data.ToPrincipal(),
      Wallet =
        data.Wallet?.ToPrincipal()
        ?? throw new ApplicationException("Inconsistent Data State: User exist without wallet."),
    };

  public static UserData ToData(this UserRecord record) =>
    new()
    {
      Username = record.Username,
      Email = record.Email,
      EmailVerified = record.EmailVerified,
      Roles = record.Roles,
      ExtraRoles = record.ExtraRoles,
    };

  public static UserData Update(this UserData data, UserRecord record)
  {
    data.Username = record.Username;
    data.Email = record.Email;
    data.EmailVerified = record.EmailVerified;
    data.Roles = record.Roles;
    // ExtraRoles is deliberately NOT copied: the frontend token sync drives
    // Update with a record built from the JWT (which knows nothing about
    // admin-granted roles) — copying it here would silently wipe them
    return data;
  }
}
