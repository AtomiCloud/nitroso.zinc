using CSharp_Result;
using Domain.Exceptions;
using Domain.Transaction;
using Domain.Wallet;

namespace Domain.Withdrawal;

public class WithdrawalService(
  IWithdrawalRepository repo,
  IWalletRepository walletRepo,
  ITransactionRepository transactionRepository,
  ITransactionGenerator generator,
  IWithdrawalStorage withdrawalStorage,
  ITransactionManager transactionManager,
  IFeeCalculator feeCalculator,
  IPayoutGateway payoutGateway
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
    return transactionManager.Start(
      () =>
        walletRepo
          .GetByUserId(userId)
          .NullToError(userId)
          .DoAwait(DoType.MapErrors, w => walletRepo.PrepareWithdraw(w.Principal.Id, record.Amount))
          .DoAwait(
            DoType.MapErrors,
            w =>
              transactionRepository.Create(
                w.Principal.Id,
                generator.CreateWithdrawalRequest(record)
              )
          )
          .ThenAwait(w => repo.Create(w.Principal.Id, record))
    );
  }

  public Task<Result<WithdrawalPrincipal>> Cancel(Guid id, string userId, string note)
  {
    return transactionManager.Start(
      () =>
        repo.Get(id, userId)
          .NullToError(id.ToString())
          .DoAwait(DoType.MapErrors, w => GuardStatus(w, WithdrawalOperations.Cancel))
          // update wallet
          .DoAwait(
            DoType.MapErrors,
            w =>
              walletRepo
                .CancelWithdraw(w.Wallet.Id, w.Principal.Record.Amount)
                .NullToError(w.Wallet.Id.ToString())
          )
          // update transaction
          .DoAwait(
            DoType.MapErrors,
            w =>
              transactionRepository.Create(
                w.Wallet.Id,
                generator.CancelWithdrawalRequest(w.Principal.Record)
              )
          )
          .ThenAwait(_ =>
            repo.Update(
                userId,
                id,
                null,
                new WithdrawalStatus { Status = WithdrawStatus.Cancel },
                new WithdrawalComplete
                {
                  Note = note,
                  Receipt = null,
                  CompletedAt = DateTime.UtcNow,
                  CompleterId = userId,
                }
              )
              .NullToError(id.ToString())
          )
    );
  }

  // only admin
  public Task<Result<WithdrawalPrincipal>> Reject(Guid id, string completerId, string note)
  {
    return transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .DoAwait(DoType.MapErrors, w => GuardStatus(w, WithdrawalOperations.Reject))
          // update wallet
          .DoAwait(
            DoType.MapErrors,
            w =>
              walletRepo
                .CancelWithdraw(w.Wallet.Id, w.Principal.Record.Amount)
                .NullToError(w.Wallet.Id.ToString())
          )
          // update transaction
          .DoAwait(
            DoType.MapErrors,
            w =>
              transactionRepository.Create(
                w.Wallet.Id,
                generator.RejectWithdrawalRequest(w.Principal.Record)
              )
          )
          // reject the withdrawal request
          .ThenAwait(_ =>
            repo.Update(
                null,
                id,
                null,
                new WithdrawalStatus { Status = WithdrawStatus.Rejected },
                new WithdrawalComplete
                {
                  Note = note,
                  Receipt = null,
                  CompletedAt = DateTime.UtcNow,
                  CompleterId = completerId,
                }
              )
              .NullToError(id.ToString())
          )
    );
  }

  public Task<Result<WithdrawalPrincipal>> Complete(
    Guid id,
    string completerId,
    string note,
    Stream receipt
  )
  {
    return transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .DoAwait(DoType.MapErrors, w => GuardStatus(w, WithdrawalOperations.Complete))
          .ThenAwait(w =>
          {
            // manual completion charges the same fee as the automated payout:
            // the admin pays out the net amount and the fee is collected into
            // the fee account, so both paths produce an identical ledger
            var fee =
              w.Principal.Payout?.Fee ?? feeCalculator.WithdrawFee(w.Principal.Record.Amount);
            return CollectReserve(w, fee)
              .ThenAwait(_ => withdrawalStorage.Save(receipt))
              .ThenAwait(link =>
                repo.Update(
                    null,
                    id,
                    null,
                    new WithdrawalStatus { Status = WithdrawStatus.Completed },
                    new WithdrawalComplete
                    {
                      Note = note,
                      Receipt = link,
                      CompletedAt = DateTime.UtcNow,
                      CompleterId = completerId,
                    },
                    new WithdrawalPayout
                    {
                      ConfirmationNumber = w.Principal.Payout?.ConfirmationNumber,
                      Fee = fee,
                      Attempt = w.Principal.Payout?.Attempt ?? 0,
                    }
                  )
                  .NullToError(id.ToString())
              );
          })
    );
  }

  public async Task<Result<WithdrawalPrincipal>> Approve(Guid id)
  {
    // Phase 1 (claim): Pending -> Processing with a fresh attempt number, in
    // ONE transaction so two concurrent approves can never both claim the
    // withdrawal. No money moves here; the reserve is collected only when the
    // gateway confirms the payout via webhook.
    var claim = await transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .DoAwait(DoType.MapErrors, w => GuardStatus(w, WithdrawalOperations.Approve))
          .ThenAwait(w =>
          {
            var payout = new WithdrawalPayout
            {
              ConfirmationNumber = w.Principal.Payout?.ConfirmationNumber,
              Fee = feeCalculator.WithdrawFee(w.Principal.Record.Amount),
              Attempt = (w.Principal.Payout?.Attempt ?? 0) + 1,
            };
            return repo.Update(
                null,
                id,
                null,
                new WithdrawalStatus { Status = WithdrawStatus.Processing },
                null,
                payout
              )
              .NullToError(id.ToString())
              .Then(_ => (Withdrawal: w, Payout: payout), Errors.MapNone);
          })
    );
    if (claim.IsFailure())
      return claim.FailureOrDefault();

    var (withdrawal, claimed) = claim.SuccessOrDefault();

    // Phase 2: create the payout at the gateway, outside any DB transaction.
    // The request id is unique per attempt, so the gateway's idempotency
    // guarantees a retried attempt cannot create a second payout.
    var created = await payoutGateway.CreatePayout(
      new PayoutRequest
      {
        RequestId = $"{id}-{claimed.Attempt}",
        Amount = withdrawal.Principal.Record.Amount - claimed.Fee,
        PayNowNumber = withdrawal.Principal.Record.PayNowNumber,
      }
    );

    if (created.IsFailure())
    {
      // Roll the claim back so the withdrawal can be retried (next attempt
      // gets a fresh request id). The attempt counter stays incremented.
      var release = await transactionManager.Start(
        () =>
          repo.Update(
              null,
              id,
              null,
              new WithdrawalStatus { Status = WithdrawStatus.Pending },
              null,
              null
            )
            .NullToError(id.ToString())
      );
      if (release.IsFailure())
        return release.FailureOrDefault();
      return created.FailureOrDefault();
    }

    // Phase 3: persist the confirmation number. If this write is lost (e.g.
    // the pod dies), the webhook still resolves the withdrawal via the
    // request id, so the payout is never orphaned.
    return await transactionManager.Start(
      () =>
        repo.Update(
            null,
            id,
            null,
            null,
            null,
            claimed with
            {
              ConfirmationNumber = created.SuccessOrDefault().Id,
            }
          )
          .NullToError(id.ToString())
    );
  }

  public Task<Result<WithdrawalPrincipal>> CompletePayout(Guid id, string confirmationNumber)
  {
    return transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .DoAwait(
            DoType.MapErrors,
            w => GuardStatus(w, WithdrawalOperations.Complete, WithdrawStatus.Processing)
          )
          .ThenAwait(w =>
          {
            var payout = w.Principal.Payout;
            if (payout == null)
              return Task.FromResult(
                (Result<WithdrawalPrincipal>)
                  new InvalidWithdrawalOperationException(
                    "Payout completion requires an approved withdrawal with payout bookkeeping",
                    w.Principal.Status.Status,
                    WithdrawalOperations.Complete
                  )
              );
            return CollectReserve(w, payout.Fee)
              .ThenAwait(_ =>
                repo.Update(
                    null,
                    id,
                    null,
                    new WithdrawalStatus { Status = WithdrawStatus.Completed },
                    new WithdrawalComplete
                    {
                      Note =
                        $"Automatically paid out via Airwallex transfer '{confirmationNumber}'",
                      Receipt = null,
                      CompletedAt = DateTime.UtcNow,
                      CompleterId = null,
                    },
                    payout with
                    {
                      ConfirmationNumber = confirmationNumber,
                    }
                  )
                  .NullToError(id.ToString())
              );
          })
    );
  }

  public Task<Result<WithdrawalPrincipal>> FailPayout(Guid id, string reason)
  {
    // Status-only: the reserve was never collected, so returning to Pending
    // makes the withdrawal eligible for another approve attempt (which will
    // use a fresh gateway request id)
    return transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .DoAwait(
            DoType.MapErrors,
            w => GuardStatus(w, WithdrawalOperations.PayoutFailed, WithdrawStatus.Processing)
          )
          .ThenAwait(_ =>
            repo.Update(
                null,
                id,
                null,
                new WithdrawalStatus { Status = WithdrawStatus.Pending },
                null,
                null
              )
              .NullToError(id.ToString())
          )
    );
  }

  public Task<Result<Unit?>> Delete(Guid id)
  {
    return repo.Delete(id);
  }

  // Collects the full reserved amount: net to BunnyBooker (paid out to the
  // user) and fee to the withdrawal-fee account, as two ledger transactions
  private Task<Result<Withdrawal>> CollectReserve(Withdrawal w, decimal fee)
  {
    return walletRepo
      .Withdraw(w.Wallet.Id, w.Principal.Record.Amount)
      .NullToError(w.Wallet.Id.ToString())
      .ThenAwait(_ =>
        transactionRepository.Create(
          w.Wallet.Id,
          generator.CompleteWithdrawalRequest(w.Principal.Record, fee)
        )
      )
      .ThenAwait(_ =>
        transactionRepository.Create(
          w.Wallet.Id,
          generator.WithdrawalFeeCharge(w.Principal.Record, fee)
        )
      )
      .Then(_ => w, Errors.MapNone);
  }

  // Withdrawal state machine guard: the operation may only fire from the
  // given statuses (defaults to Pending). Runs inside the surrounding
  // RepeatableRead transaction so the read cannot go stale.
  private static Task<Result<int>> GuardStatus(
    Withdrawal w,
    string operation,
    params WithdrawStatus[] allowed
  )
  {
    if (allowed.Length == 0)
      allowed = [WithdrawStatus.Pending];
    if (allowed.Contains(w.Principal.Status.Status))
      return Task.FromResult((Result<int>)0);
    var r = new InvalidWithdrawalOperationException(
      $"{operation} requires the withdrawal to be in status(es): {string.Join(", ", allowed)}",
      w.Principal.Status.Status,
      operation
    );
    return Task.FromResult((Result<int>)r);
  }
}
