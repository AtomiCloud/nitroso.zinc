using App.Modules.Users.API.V1;
using Domain.Wallet;

namespace App.Modules.Wallets.API.V1;

public static class WalletMapper
{
  // RES
  public static WalletPrincipalRes ToRes(this WalletPrincipal walletPrincipal)
    => new(walletPrincipal.Id, walletPrincipal.Record.Usable, walletPrincipal.Record.WithdrawReserve,
      walletPrincipal.Record.BookingReserve);

  public static WalletRes ToRes(this Wallet wallet)
    => new(wallet.Principal.ToRes(), wallet.User.ToRes());

  // REQ
  public static WalletSearch ToDomain(this SearchWalletQuery query) =>
    new()
    {
      Id = query.Id,
      UserId = query.UserId,
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };
}
