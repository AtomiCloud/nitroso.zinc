using App.Modules.Common;
using App.Modules.Wallets.API.V1;
using App.Utility;
using Domain.User;

namespace App.Modules.Users.API.V1;

public static class UserMapper
{
  // RES
  public static UserPrincipalRes ToRes(this UserPrincipal userPrincipal) =>
    new(userPrincipal.Id, userPrincipal.Record.Username, userPrincipal.Record.Email,
      userPrincipal.Record.EmailVerified, userPrincipal.Record.Roles,
      userPrincipal.Record.ExtraRoles);

  public static UserRes ToRes(this User user) => new(user.Principal.ToRes(), user.Wallet.ToRes());

  public static PartnerUserRes ToRes(this PartnerUser user) =>
    new(user.Id, user.Username, user.Email);

  public static UserWipeRes ToRes(this UserWipe wipe) => new(wipe.Id, wipe.WipedAt);

  public static PartnerPnlRowRes ToRes(this PartnerPnlRow row) =>
    new(
      row.Month,
      row.Bookings,
      row.Collected,
      row.KtmbCost,
      row.Deposits,
      row.WithdrawalGross,
      row.WithdrawalFeeIncome,
      row.BoostCount,
      row.BoostAmount,
      row.DistinctPassengers
    );

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

  public static PartnerPnlQuery ToDomain(this PartnerPnlQueryReq query) =>
    new() { After = query.After?.ToDate(), Before = query.Before?.ToDate() };
}
