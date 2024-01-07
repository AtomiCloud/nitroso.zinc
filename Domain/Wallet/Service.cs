using CSharp_Result;

namespace Domain.Wallet;

public class WalletService(IWalletRepository repo) : IWalletService
{
  public Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<Wallet?>> Get(Guid id, string? userId)
  {
    return repo.Get(id, userId);
  }

  public Task<Result<Wallet?>> GetByUserId(string userId)
  {
    return repo.GetByUserId(userId);
  }
}
