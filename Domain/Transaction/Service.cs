using CSharp_Result;

namespace Domain.Transaction;

public class TransactionService(ITransactionRepository repo) : ITransactionService
{
  public Task<Result<IEnumerable<TransactionPrincipal>>> Search(TransactionSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<Transaction?>> Get(Guid id, string? userId)
  {
    return repo.Get(id, userId);
  }

  public Task<Result<TransactionPrincipal>> Create(Guid walletId, TransactionRecord record)
  {
    return repo.Create(walletId, record);
  }

  public Task<Result<Unit?>> Delete(Guid id)
  {
    return repo.Delete(id);
  }
}
