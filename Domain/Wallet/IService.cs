using CSharp_Result;

namespace Domain.Wallet;

public interface IWalletService
{
  Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search);

  Task<Result<Wallet?>> Get(Guid id, string? userId);

  Task<Result<Wallet?>> GetByUserId(string userId);
}
