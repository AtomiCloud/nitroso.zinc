using CSharp_Result;
using Domain.Transaction;
using Domain.Wallet;

namespace Domain.Admin;

public class AdminService(
  IWalletRepository walletRepo,
  ITransactionRepository transactionRepo,
  ITransactionGenerator transactionGenerator,
  ITransactionManager transaction
) : IAdminService
{
  public Task<Result<WalletPrincipal>> TransferIn(string userId, decimal amount, string desc)
  {
    return transaction.Start(
      () =>
        walletRepo
          .GetByUserId(userId)
          .NullToError(userId)
          .ThenAwait(w => walletRepo.Deposit(w.Principal.Id, amount))
          .NullToError(userId)
          .DoAwait(
            DoType.MapErrors,
            w => transactionRepo.Create(w.Id, transactionGenerator.AdminInflow(amount, desc))
          )
    );
  }

  public Task<Result<WalletPrincipal>> TransferOut(string userId, decimal amount, string desc)
  {
    return transaction.Start(
      () =>
        walletRepo
          .GetByUserId(userId)
          .NullToError(userId)
          .ThenAwait(w => walletRepo.Collect(w.Principal.Id, amount))
          .NullToError(userId)
          .DoAwait(
            DoType.MapErrors,
            w => transactionRepo.Create(w.Id, transactionGenerator.AdminOutflow(amount, desc))
          )
    );
  }

  public Task<Result<WalletPrincipal>> Promo(string userId, decimal amount, string desc)
  {
    return transaction.Start(
      () =>
        walletRepo
          .GetByUserId(userId)
          .NullToError(userId)
          .ThenAwait(w => walletRepo.Deposit(w.Principal.Id, amount))
          .NullToError(userId)
          .DoAwait(
            DoType.MapErrors,
            w => transactionRepo.Create(w.Id, transactionGenerator.Promotional(amount, desc))
          )
    );
  }
}
