using CSharp_Result;

namespace Domain.Transaction;

public interface ITransactionRepository
{
  Task<Result<IEnumerable<TransactionPrincipal>>> Search(TransactionSearch search);

  Task<Result<Transaction?>> Get(Guid id, string? userId);

  Task<Result<TransactionPrincipal>> Create(Guid walletId, TransactionRecord record);

  Task<Result<Unit?>> Delete(Guid id);
}
