using CSharp_Result;

namespace Domain.Wallet;

public interface IWalletRepository
{
  Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search);

  Task<Result<Wallet?>> Get(Guid id, string? userId);

  Task<Result<Wallet?>> GetByUserId(string userId);

  // usable -> withdraw reserve
  Task<Result<WalletPrincipal?>> PrepareWithdraw(Guid id, decimal amount);

  // deduce withdraw reserve
  Task<Result<WalletPrincipal?>> Withdraw(Guid id, decimal amount);

  // increase usable
  Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount);

  // decrease usable
  Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount);

  // usable -> booking reserve
  Task<Result<WalletPrincipal?>> BookStart(Guid id, decimal amount);

  // revert: booking reserve -> usable
  // collect: deduct booking reserve
  Task<Result<WalletPrincipal?>> BookEnd(Guid id, decimal revert, decimal collect);

}
