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
    // withdrawal. A withdrawal already Processing WITHOUT a confirmation
    // number may be re-driven: it keeps its attempt — and therefore its
    // gateway request id — so the gateway's idempotency collapses the retry
    // into the original transfer instead of paying twice. No money moves
    // here; the reserve is collected only when the gateway confirms via
    // webhook.
    var claim = await transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .ThenAwait(w =>
          {
            var status = w.Principal.Status.Status;
            var redrive =
              status == WithdrawStatus.Processing
              && w.Principal.Payout is { ConfirmationNumber: null };
            if (status != WithdrawStatus.Pending && !redrive)
            {
              var r = new InvalidWithdrawalOperationException(
                "Approve requires the withdrawal to be 'Pending', or 'Processing' without a confirmation number (re-drive)",
                status,
                WithdrawalOperations.Approve
              );
              return Task.FromResult((Result<(Withdrawal, WithdrawalPayout, bool)>)r);
            }
            var payout = redrive
              ? w.Principal.Payout!
              : new WithdrawalPayout
              {
                ConfirmationNumber = null,
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
              .Then(_ => (w, payout, redrive), Errors.MapNone);
          })
    );
    if (claim.IsFailure())
      return claim.FailureOrDefault();

    var (withdrawal, claimed, redrive) = claim.SuccessOrDefault();

    // Phase 2: create the payout at the gateway, outside any DB transaction.
    // The request id is deterministic per attempt, so a re-send of the same
    // attempt can never create a second payout.
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
      var failure = created.FailureOrDefault();
      // Only a definitive gateway rejection of a FIRST send proves no
      // transfer exists — that alone may return the withdrawal to Pending
      // (whose next approve mints a fresh attempt). Everything else is
      // ambiguous — timeout, 5xx, or any failure of a re-send (a 4xx there
      // may just be the gateway deduplicating the original request id) — so
      // the withdrawal stays Processing and is re-driven later with the SAME
      // request id, or resolved by the original transfer's webhook.
      if (!redrive && failure is PayoutRejectedException)
      {
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
      }
      return failure;
    }

    // Phase 3: persist the confirmation number. If this write is lost (e.g.
    // the pod dies), the webhook normally resolves the withdrawal via the
    // request id; should the webhook ALSO be lost permanently, the admin
    // force-complete endpoint is the escape hatch (see ForceCompletePayout).
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

  public Task<Result<WithdrawalPrincipal>> CompletePayout(
    Guid id,
    string confirmationNumber,
    int? attempt
  )
  {
    return transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .ThenAwait(w =>
          {
            var payout = w.Principal.Payout;
            var status = w.Principal.Status.Status;

            // idempotent redelivery: this exact transfer already completed
            // the withdrawal, acknowledge without moving money again
            if (
              status == WithdrawStatus.Completed
              && payout?.ConfirmationNumber == confirmationNumber
            )
              return Task.FromResult((Result<WithdrawalPrincipal>)w.Principal);

            if (status != WithdrawStatus.Processing)
              return Stale(
                $"settled event for transfer '{confirmationNumber}' but withdrawal '{id}' is '{status}'"
              );

            if (payout == null)
              return Task.FromResult(
                (Result<WithdrawalPrincipal>)
                  new InvalidWithdrawalOperationException(
                    "Payout completion requires an approved withdrawal with payout bookkeeping",
                    status,
                    WithdrawalOperations.CompletePayout
                  )
              );

            // an event for a superseded attempt must never settle the current
            // one — its transfer is not the money that is in flight now
            if (attempt != null && attempt != payout.Attempt)
              return Stale(
                $"settled event for attempt {attempt} of withdrawal '{id}', current attempt is {payout.Attempt}"
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

  // Admin escape hatch for a confirmed Processing withdrawal whose settled
  // webhook was permanently lost: after verifying the transfer on the
  // Airwallex dashboard, an admin finalizes it through the same idempotent
  // path the webhook would have taken (the transactional Processing guard
  // still prevents any race with a late webhook).
  public Task<Result<WithdrawalPrincipal>> ForceCompletePayout(Guid id)
  {
    return repo.Get(id, null)
      .NullToError(id.ToString())
      .ThenAwait(w =>
      {
        var payout = w.Principal.Payout;
        if (
          w.Principal.Status.Status != WithdrawStatus.Processing
          || payout?.ConfirmationNumber == null
        )
          return Task.FromResult(
            (Result<WithdrawalPrincipal>)
              new InvalidWithdrawalOperationException(
                "Force-completion requires a 'Processing' withdrawal with a confirmed transfer",
                w.Principal.Status.Status,
                WithdrawalOperations.CompletePayout
              )
          );
        return this.CompletePayout(id, payout.ConfirmationNumber, payout.Attempt);
      });
  }

  public Task<Result<WithdrawalPrincipal>> FailPayout(Guid id, string reason, int? attempt)
  {
    // Status-only: the reserve was never collected, so returning to Pending
    // makes the withdrawal eligible for another approve attempt (which mints
    // a fresh gateway request id — safe, because the gateway reported this
    // attempt's transfer as terminally failed)
    return transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .ThenAwait(w =>
          {
            var status = w.Principal.Status.Status;
            var payout = w.Principal.Payout;

            // idempotent redelivery: an earlier failure event already
            // returned it to Pending
            if (status == WithdrawStatus.Pending)
              return Task.FromResult((Result<WithdrawalPrincipal>)w.Principal);

            if (status != WithdrawStatus.Processing)
              return Stale($"failure event for withdrawal '{id}' in '{status}': {reason}");

            // a failure event for a superseded attempt must not release the
            // claim of the attempt currently in flight
            if (payout != null && attempt != null && attempt != payout.Attempt)
              return Stale(
                $"failure event for attempt {attempt} of withdrawal '{id}', current attempt is {payout.Attempt}"
              );

            // the confirmation number belongs to the transfer that just
            // failed; clear it so later flows can never present a dead
            // transfer as proof of payment (attempt and fee are kept — the
            // attempt counter guarantees request-id uniqueness)
            return repo.Update(
                null,
                id,
                null,
                new WithdrawalStatus { Status = WithdrawStatus.Pending },
                null,
                payout == null ? null : payout with { ConfirmationNumber = null }
              )
              .NullToError(id.ToString());
          })
    );
  }

  private static Task<Result<WithdrawalPrincipal>> Stale(string message) =>
    Task.FromResult((Result<WithdrawalPrincipal>)new StalePayoutEventException(message));

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
