using CSharp_Result;
using Domain.Transaction;
using Domain.Wallet;

namespace Domain.Withdrawal;

public class WithdrawalService(
  IWithdrawalRepository repo,
  IWalletRepository walletRepo,
  ITransactionRepository transactionRepository,
  ITransactionGenerator generator,
  IWithdrawalStorage withdrawalStorage,
  ITransactionManager transactionManager
) : IWithdrawalService
{
  public Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<Withdrawal?>> Get(Guid id, string? userId)
  {
    return repo.Get(id, userId);
  }

  public Task<Result<WithdrawalPrincipal>> Create(string userId, WithdrawalRecord record)
  {
    return transactionManager.Start(() => walletRepo
        .GetByUserId(userId)
        .NullToError(userId)
        .DoAwait(DoType.MapErrors, w => walletRepo.PrepareWithdraw(w.Principal.Id, record.Amount))
        .DoAwait(DoType.MapErrors,
          w => transactionRepository.Create(w.Principal.Id, generator.CreateWithdrawalRequest(record)))
        .ThenAwait(w => repo.Create(w.Principal.Id, record))
    );
  }

  public Task<Result<WithdrawalPrincipal>> Cancel(Guid id, string userId, string note)
  {
    return transactionManager.Start(() => repo.Get(id, userId)
      .NullToError(id.ToString())
      // update wallet
      .DoAwait(DoType.MapErrors, w =>
        walletRepo.CancelWithdraw(w.Wallet.Id, w.Principal.Record.Amount).NullToError(w.Wallet.Id.ToString())
      )
      // update transaction
      .DoAwait(DoType.MapErrors,
        w => transactionRepository.Create(w.Wallet.Id, generator.CancelWithdrawalRequest(w.Principal.Record)))
      .ThenAwait(_ => repo.Update(userId, id, null,
        new WithdrawalStatus { Status = WithdrawStatus.Cancel },
        new WithdrawalComplete
        {
          Note = note,
          Receipt = null,
          CompletedAt = DateTime.UtcNow,
          CompleterId = userId,
        })
        .NullToError(id.ToString())
      ));
  }

  // only admin
  public Task<Result<WithdrawalPrincipal>> Reject(Guid id, string completerId, string note)
  {
    return transactionManager.Start(() => repo.Get(id, null)
      .NullToError(id.ToString())
      // update wallet
      .DoAwait(DoType.MapErrors, w =>
        walletRepo.CancelWithdraw(w.Wallet.Id, w.Principal.Record.Amount)
          .NullToError(w.Wallet.Id.ToString())
      )
      // update transaction
      .DoAwait(DoType.MapErrors,
        w => transactionRepository
          .Create(w.Wallet.Id, generator.RejectWithdrawalRequest(w.Principal.Record)))
      // reject the withdrawal request
      .ThenAwait(_ => repo.Update(null, id, null,
        new WithdrawalStatus { Status = WithdrawStatus.Rejected },
        new WithdrawalComplete
        {
          Note = note,
          Receipt = null,
          CompletedAt = DateTime.UtcNow,
          CompleterId = completerId,
        })
        .NullToError(id.ToString())
      ));
  }

  public Task<Result<WithdrawalPrincipal>> Complete(Guid id, string completerId, string note, Stream receipt)
  {
    return transactionManager.Start(() => repo.Get(id, null)
      .NullToError(id.ToString())
      // update wallet
      .DoAwait(DoType.MapErrors, w =>
        walletRepo.Withdraw(w.Wallet.Id, w.Principal.Record.Amount).NullToError(w.Wallet.Id.ToString())
      )
      // update transaction
      .DoAwait(DoType.MapErrors,
        w => transactionRepository.Create(w.Wallet.Id, generator
          .CompleteWithdrawalRequest(w.Principal.Record))
        )
      .ThenAwait(_ => withdrawalStorage.Save(receipt))
      .ThenAwait(link => repo.Update(null, id, null,
        new WithdrawalStatus { Status = WithdrawStatus.Completed },
        new WithdrawalComplete
        {
          Note = note,
          Receipt = link,
          CompletedAt = DateTime.UtcNow,
          CompleterId = completerId,
        })
        .NullToError(id.ToString())
      ));
  }

  public Task<Result<Unit?>> Delete(Guid id)
  {
    return repo.Delete(id);
  }
}
