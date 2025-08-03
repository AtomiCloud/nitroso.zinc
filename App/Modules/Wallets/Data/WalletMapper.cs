using App.Modules.Users.Data;
using Domain.Wallet;

namespace App.Modules.Wallets.Data;

public static class WalletMapper
{
  // Data -> Domain
  public static WalletRecord ToRecord(this WalletData principal) =>
    new()
    {
      Usable = principal.Usable,
      WithdrawReserve = principal.WithdrawReserve,
      BookingReserve = principal.BookingReserve,
    };

  public static WalletPrincipal ToPrincipal(this WalletData data) =>
    new()
    {
      Id = data.Id,
      UserId = data.UserId,
      Record = data.ToRecord(),
    };

  public static Wallet ToDomain(this WalletData data) =>
    new() { Principal = data.ToPrincipal(), User = data.User.ToPrincipal() };

  // Domain -> Data
  public static WalletData ToData(this WalletRecord record) =>
    new()
    {
      BookingReserve = record.BookingReserve,
      WithdrawReserve = record.WithdrawReserve,
      Usable = record.Usable,
    };

  public static WalletData Update(this WalletData data, WalletRecord record)
  {
    data.BookingReserve = record.BookingReserve;
    data.WithdrawReserve = record.WithdrawReserve;
    data.Usable = record.Usable;
    return data;
  }
}
