using CSharp_Result;
using Domain.Wallet;

namespace Domain.Admin;

public interface IAdminService
{
  Task<Result<WalletPrincipal>> TransferIn(string userId, decimal amount, string desc);

  Task<Result<WalletPrincipal>> TransferOut(string userId, decimal amount, string desc);

  Task<Result<WalletPrincipal>> Promo(string userId, decimal amount, string desc);
}
