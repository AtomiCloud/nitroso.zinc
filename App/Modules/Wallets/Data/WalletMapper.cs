using App.Modules.Users.Data;
using Domain.Wallet;

namespace App.Modules.Wallets.Data;

public static class WalletMapper
{
  public static WalletRecord ToRecord(this WalletData principal) => new()
  {
    Usable = principal.Usable,
    WithdrawReserve = principal.WithdrawReserve,
    BookingReserve = principal.BookingReserve,
  };

  public static WalletPrincipal ToPrincipal(this WalletData data) => new()
  {
    Id = data.Id,
    Record = data.ToRecord(),
  };


  public static Wallet ToDomain(this WalletData data) => new()
  {
    Principal = data.ToPrincipal(),
    User = data.User.ToPrincipal(),
  };

  public static WalletData ToData(this WalletRecord record) => new()
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
