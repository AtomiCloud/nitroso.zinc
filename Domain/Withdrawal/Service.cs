using CSharp_Result;
using Domain.Exceptions;
using Domain.Transaction;
using Domain.Wallet;
using WalletAggregate = Domain.Wallet.Wallet;

namespace Domain.Withdrawal;

public class WithdrawalService(
  IWithdrawalRepository repo,
  IWalletRepository walletRepo,
  ITransactionRepository transactionRepository,
  ITransactionGenerator generator,
  IWithdrawalStorage withdrawalStorage,
  ITransactionManager transactionManager,
  IFeeCalculator feeCalculator,
  IPayoutGateway payoutGateway,
  IWithdrawalRefundRepository refundRepo,
  IRefundGateway refundGateway,
  IWithdrawalSettingsRepository settingsRepo
) : IWithdrawalService
{
  private const int DefaultRefundWindowDays = IWithdrawalService.DefaultRefundWindowDays;

  public Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<Withdrawal?>> Get(Guid id, string? userId)
  {
    return repo.Get(id, userId);
  }

  public Task<Result<decimal>> RefundablePool(
    string userId,
    int refundWindowDays = DefaultRefundWindowDays
  )
  {
    return walletRepo
      .GetByUserId(userId)
      .NullToError(userId)
      .ThenAwait(w => this.ComputePool(w.Principal.Id, refundWindowDays))
      .Then(pool => pool.Sum(x => x.Refundable), Errors.MapNone);
  }

  public Task<Result<WithdrawalPrincipal>> Create(
    string userId,
    WithdrawalRecord record,
    int refundWindowDays = DefaultRefundWindowDays
  )
  {
    return transactionManager.Start(
      () =>
        walletRepo
          .GetByUserId(userId)
          .NullToError(userId)
          // method policy: the admin-configured withdrawal settings decide
          // which rails accept NEW withdrawals right now (existing
          // withdrawals are unaffected — approve/webhooks run regardless)
          .ThenAwait(w => this.GuardMethodPolicy(w, record, refundWindowDays))
          // a card-refund withdrawal is only accepted when the refundable
          // pool covers the net amount — checked BEFORE the reserve moves so
          // a hopeless request never locks the user's funds. The pool is
          // re-checked at approval; this uses the fee rate current now.
          .ThenAwait(async w =>
          {
            if (record.Method != WithdrawalMethod.CardRefund)
              return (Result<WalletAggregate>)w;
            var feeR = await feeCalculator.Compute(FeeType.Withdrawal, record.Amount);
            if (feeR.IsFailure())
              return (Result<WalletAggregate>)feeR.FailureOrDefault();
            var net = record.Amount - feeR.SuccessOrDefault();
            var poolR = await this.ComputePool(w.Principal.Id, refundWindowDays);
            if (poolR.IsFailure())
              return (Result<WalletAggregate>)poolR.FailureOrDefault();
            var pool = poolR.SuccessOrDefault().Sum(x => x.Refundable);
            if (pool < net)
              return (Result<WalletAggregate>)
                new InsufficientRefundablePoolException(
                  $"The refundable pool (SGD {pool:0.00}) does not cover the net withdrawal amount (SGD {net:0.00})",
                  net,
                  pool
                );
            return (Result<WalletAggregate>)w;
          })
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

  // Method policy for NEW withdrawals, from the admin-configured settings
  // (defaults when never configured): CardRefund requires the rail to be
  // enabled; PayNow is accepted when Enabled, or under FallbackOnly only
  // when the refundable pool cannot cover the requested amount (the pool is
  // computed once, only when FallbackOnly makes it relevant). Rejections are
  // distinguishable invalid-operation errors so the UI can explain why.
  private async Task<Result<WalletAggregate>> GuardMethodPolicy(
    WalletAggregate w,
    WithdrawalRecord record,
    int refundWindowDays
  )
  {
    var settingsR = await settingsRepo.GetCurrent();
    if (settingsR.IsFailure())
      return settingsR.FailureOrDefault();
    var settings = settingsR.SuccessOrDefault()?.Record ?? WithdrawalSettingsRecord.Default;

    if (record.Method == WithdrawalMethod.CardRefund)
    {
      if (!settings.CardRefundEnabled)
        return new InvalidWithdrawalOperationException(
          "Card-refund withdrawals are currently disabled — use PayNow instead",
          WithdrawStatus.Pending,
          WithdrawalOperations.Create
        );
      return w;
    }

    switch (settings.PayNowMode)
    {
      case PayNowMode.Enabled:
        return w;
      case PayNowMode.Disabled:
        return new InvalidWithdrawalOperationException(
          "PayNow withdrawals are currently disabled — use a card refund instead",
          WithdrawStatus.Pending,
          WithdrawalOperations.Create
        );
      default:
        // FallbackOnly: PayNow only carries what the card rail cannot cover
        var poolR = await this.ComputePool(w.Principal.Id, refundWindowDays);
        if (poolR.IsFailure())
          return poolR.FailureOrDefault();
        var pool = poolR.SuccessOrDefault().Sum(x => x.Refundable);
        if (pool < record.Amount)
          return w;
        return new InvalidWithdrawalOperationException(
          $"A card refund can cover this amount (refundable pool SGD {pool:0.00}) — PayNow is available only as a fallback when card refunds cannot cover the withdrawal",
          WithdrawStatus.Pending,
          WithdrawalOperations.Create
        );
    }
  }

  // The wallet's refundable pool: captured Airwallex card payments inside the
  // window, oldest first, each minus the refunds already issued against it
  // (any withdrawal, Created or Settled — only Failed fragments release
  // their claim on the intent)
  private Task<Result<List<RefundablePayment>>> ComputePool(Guid walletId, int refundWindowDays)
  {
    var since = DateTime.UtcNow.AddDays(-refundWindowDays);
    return refundRepo
      .ListFundingPayments(walletId, since)
      .ThenAwait(payments =>
        refundRepo
          .SumActiveRefundsByPayment(payments.Select(p => p.PaymentId))
          .Then(
            refunded =>
              payments
                .Select(p => new RefundablePayment
                {
                  PaymentId = p.PaymentId,
                  PaymentIntentId = p.PaymentIntentId,
                  CreatedAt = p.CreatedAt,
                  Refundable = p.CapturedAmount - refunded.GetValueOrDefault(p.PaymentId),
                })
                .Where(p => p.Refundable > 0)
                .OrderBy(p => p.CreatedAt)
                .ToList(),
            Errors.MapNone
          )
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

  // only admin. Also allowed from RequireManualIntervention: the admin has
  // verified at the gateway that no money left, so refunding is safe.
  public Task<Result<WithdrawalPrincipal>> Reject(Guid id, string completerId, string note)
  {
    return transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .DoAwait(
            DoType.MapErrors,
            w =>
              GuardStatus(
                w,
                WithdrawalOperations.Reject,
                WithdrawStatus.Pending,
                WithdrawStatus.RequireManualIntervention
              )
          )
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
          .ThenAwait(async w =>
          {
            // manual completion charges the same fee as the automated payout:
            // the admin pays out the net amount and the fee is collected into
            // the fee account, so both paths produce an identical ledger
            var feeR =
              w.Principal.Payout != null
                ? (Result<decimal>)w.Principal.Payout.Fee
                : await feeCalculator.Compute(FeeType.Withdrawal, w.Principal.Record.Amount);
            if (feeR.IsFailure())
              return (Result<WithdrawalPrincipal>)feeR.FailureOrDefault();
            var fee = feeR.SuccessOrDefault();
            // a flat fee can swallow a small withdrawal whole; paying out
            // SGD 0 while collecting the full amount as fee is never right —
            // the admin should reject the withdrawal instead
            if (w.Principal.Payout == null && fee >= w.Principal.Record.Amount)
              return (Result<WithdrawalPrincipal>)
                new InvalidWithdrawalOperationException(
                  $"The fee (SGD {fee:0.00}) equals or exceeds the withdrawal amount, leaving nothing to pay out — reject the withdrawal instead",
                  w.Principal.Status.Status,
                  WithdrawalOperations.Complete
                );
            return await (CollectReserve(w, fee)
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
              ));
          })
    );
  }

  public async Task<Result<WithdrawalPrincipal>> Approve(
    Guid id,
    int refundWindowDays = DefaultRefundWindowDays
  )
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
            var payoutR = redrive
              ? Task.FromResult((Result<WithdrawalPayout>)w.Principal.Payout!)
              : feeCalculator
                .Compute(FeeType.Withdrawal, w.Principal.Record.Amount)
                .ThenAwait(fee =>
                {
                  // a flat fee can swallow a small withdrawal whole; never
                  // send a zero-net transfer to the gateway — the admin
                  // should reject the withdrawal instead
                  if (fee >= w.Principal.Record.Amount)
                    return Task.FromResult<Result<WithdrawalPayout>>(
                      new InvalidWithdrawalOperationException(
                        $"The fee (SGD {fee:0.00}) equals or exceeds the withdrawal amount, leaving nothing to pay out — reject the withdrawal instead",
                        status,
                        WithdrawalOperations.Approve
                      )
                    );
                  return Task.FromResult<Result<WithdrawalPayout>>(
                    new WithdrawalPayout
                    {
                      ConfirmationNumber = null,
                      Fee = fee,
                      Attempt = (w.Principal.Payout?.Attempt ?? 0) + 1,
                    }
                  );
                });
            return payoutR.ThenAwait(payout =>
              repo.Update(
                  null,
                  id,
                  null,
                  new WithdrawalStatus { Status = WithdrawStatus.Processing },
                  null,
                  payout
                )
                .NullToError(id.ToString())
                .Then(_ => (w, payout, redrive), Errors.MapNone)
            );
          })
    );
    if (claim.IsFailure())
      return claim.FailureOrDefault();

    var (withdrawal, claimed, redrive) = claim.SuccessOrDefault();

    // Phase 2 branches on the withdrawal method: PayNow creates a single
    // transfer, CardRefund fans out into refund fragments against the
    // payments that funded the wallet
    if (withdrawal.Principal.Record.Method == WithdrawalMethod.CardRefund)
      return await this.ApproveCardRefund(id, withdrawal, claimed, refundWindowDays);

    // PayNow: create the payout at the gateway, outside any DB transaction.
    // The request id is deterministic per attempt, so a re-send of the same
    // attempt can never create a second payout.
    var created = await payoutGateway.CreatePayout(
      new PayoutRequest
      {
        RequestId = $"{id}-{claimed.Attempt}",
        Amount = withdrawal.Principal.Record.Amount - claimed.Fee,
        PayNowNumber = withdrawal.Principal.Record.PayNowNumber
          ?? throw new InvalidOperationException(
            $"PayNow withdrawal '{id}' has no PayNow number — inconsistent state"
          ),
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

    // Phase 3: persist the confirmation number — conditionally. The claim may
    // have been legitimately released or superseded while the gateway call
    // was in flight (e.g. a fast transfer.failed webhook already returned the
    // withdrawal to Pending, or a re-approve minted a newer attempt); writing
    // the stale phase-1 snapshot over that state would resurrect a dead
    // confirmation or strand the live attempt. If the write is skipped or
    // lost (e.g. the pod dies), the webhook normally resolves the withdrawal
    // via the request id; should the webhook ALSO be lost permanently, the
    // admin force-complete endpoint is the escape hatch (ForceCompletePayout).
    return await transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .ThenAwait(w =>
          {
            var payout = w.Principal.Payout;
            if (
              w.Principal.Status.Status != WithdrawStatus.Processing
              || payout == null
              || payout.Attempt != claimed.Attempt
              || payout.ConfirmationNumber != null
            )
              return Task.FromResult((Result<WithdrawalPrincipal>)w.Principal);
            return repo.Update(
                null,
                id,
                null,
                null,
                null,
                payout with
                {
                  ConfirmationNumber = created.SuccessOrDefault().Id,
                }
              )
              .NullToError(id.ToString());
          })
    );
  }

  // Card-refund phase 2: plan the net amount into refund fragments against
  // the wallet's funding payments (oldest first), persist the evidence rows,
  // then create one gateway refund per fragment. Request ids are
  // deterministic ("{id}-{attempt}-{index}"), so a partial failure leaves the
  // withdrawal Processing and a re-drive re-sends the SAME ids — the
  // gateway's idempotency collapses them into the original refunds.
  // (No redrive flag needed here, unlike the PayNow rail: the fragment rows
  // themselves distinguish a first send from a re-drive.)
  private async Task<Result<WithdrawalPrincipal>> ApproveCardRefund(
    Guid id,
    Withdrawal withdrawal,
    WithdrawalPayout claimed,
    int refundWindowDays
  )
  {
    var prefix = $"{id}-{claimed.Attempt}-";

    // A re-drive may already own fragment rows for this attempt — reuse them
    // verbatim. Re-planning would double-subtract them from the pool, and the
    // rows are the source of truth for the request ids already (possibly)
    // sent to the gateway: rows are committed BEFORE any gateway call, so
    // no rows means no refunds exist for this attempt.
    var existingR = await refundRepo.ListByWithdrawal(id);
    if (existingR.IsFailure())
      return existingR.FailureOrDefault();
    var fragments = existingR
      .SuccessOrDefault()
      .Where(f => f.RequestId.StartsWith(prefix, StringComparison.Ordinal))
      .OrderBy(f => f.RequestId, StringComparer.Ordinal)
      .ToList();

    if (fragments.Count == 0)
    {
      // plan fresh: the pool may have shrunk since creation (other card
      // withdrawals settled against the same payments), so re-check before
      // any money-adjacent action
      var net = withdrawal.Principal.Record.Amount - claimed.Fee;
      var poolR = await this.ComputePool(withdrawal.Wallet.Id, refundWindowDays);
      if (poolR.IsFailure())
        return poolR.FailureOrDefault();
      var planR = RefundPlanner.Plan(net, poolR.SuccessOrDefault());
      if (planR.IsFailure())
      {
        // Insufficient pool: no refund was created (rows precede gateway
        // calls), so releasing the claim back to Pending is safe — mirrors
        // the PayoutRejectedException bounce. The distinguishable error
        // carries the shortfall for the admin; a sweep sees a failure and
        // moves on instead of hot-looping.
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
        return planR.FailureOrDefault();
      }

      var now = DateTime.UtcNow;
      var planned = planR
        .SuccessOrDefault()
        .Select(
          (x, idx) =>
            new WithdrawalRefundFragment
            {
              Id = Guid.NewGuid(),
              WithdrawalId = id,
              PaymentId = x.Payment.PaymentId,
              PaymentIntentId = x.Payment.PaymentIntentId,
              AirwallexRefundId = null,
              RequestId = $"{prefix}{idx}",
              Amount = x.Amount,
              Status = RefundFragmentStatus.Created,
              CreatedAt = now,
              SettledAt = null,
            }
        )
        .ToList();
      var createdR = await transactionManager.Start(() => refundRepo.CreateMany(planned));
      if (createdR.IsFailure())
        return createdR.FailureOrDefault();
      fragments = createdR.SuccessOrDefault();
    }

    // create the refunds at the gateway, outside any DB transaction; refunds
    // already created (re-drive) are skipped by their stored refund id
    foreach (var fragment in fragments)
    {
      if (fragment.AirwallexRefundId != null || fragment.Status == RefundFragmentStatus.Failed)
        continue;
      var created = await refundGateway.CreateRefund(
        new RefundRequest
        {
          RequestId = fragment.RequestId,
          PaymentIntentId = fragment.PaymentIntentId,
          Amount = fragment.Amount,
        }
      );
      if (created.IsFailure())
        // ANY failure mid-fragmenting is treated as ambiguous: fragments
        // already created at the gateway stand, so the withdrawal must stay
        // Processing and be re-driven with the same request ids (or resolved
        // by webhooks/reconciliation) — never bounced to Pending, which
        // would mint a fresh attempt and double-refund
        return created.FailureOrDefault();
      var stored = await refundRepo.Update(
        fragment.Id,
        null,
        created.SuccessOrDefault().Id,
        null
      );
      if (stored.IsFailure())
        return stored.FailureOrDefault();
    }

    // Phase 3 (conditional, like the PayNow rail): the confirmation number of
    // a card withdrawal is the FIRST fragment's gateway refund id — the
    // remaining ids live on the fragment evidence rows. Written only once all
    // fragments hold a refund id, so Approve's re-drive (which keys on a null
    // confirmation) stays available until fragmenting genuinely finished.
    var confirmationsR = await refundRepo.ListByWithdrawal(id);
    if (confirmationsR.IsFailure())
      return confirmationsR.FailureOrDefault();
    var confirmed = confirmationsR
      .SuccessOrDefault()
      .Where(f => f.RequestId.StartsWith(prefix, StringComparison.Ordinal))
      .OrderBy(f => f.RequestId, StringComparer.Ordinal)
      .ToList();
    if (confirmed.Count == 0 || confirmed.Any(f => f.AirwallexRefundId == null))
      return await repo.Get(id, null).NullToError(id.ToString()).Then(w => w.Principal, Errors.MapNone);
    var confirmation = confirmed[0].AirwallexRefundId!;

    return await transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .ThenAwait(w =>
          {
            var payout = w.Principal.Payout;
            if (
              w.Principal.Status.Status != WithdrawStatus.Processing
              || payout == null
              || payout.Attempt != claimed.Attempt
              || payout.ConfirmationNumber != null
            )
              return Task.FromResult((Result<WithdrawalPrincipal>)w.Principal);
            return repo.Update(
                null,
                id,
                null,
                null,
                null,
                payout with
                {
                  ConfirmationNumber = confirmation,
                }
              )
              .NullToError(id.ToString());
          })
    );
  }

  public async Task<Result<WithdrawalPrincipal>> SettleRefundFragment(
    Guid id,
    string requestId,
    string refundId,
    int? attempt
  )
  {
    // Step 1 (own transaction): record the evidence. The settlement is real
    // money regardless of any staleness fencing below — committing it first
    // means a stale event's rollback can never erase what actually happened,
    // and the fragment keeps counting against the refundable pool.
    var recorded = await transactionManager.Start(
      () =>
        refundRepo
          .GetByRequestId(requestId)
          .ThenAwait(fragment =>
          {
            if (fragment == null)
              return Task.FromResult(
                (Result<WithdrawalRefundFragment?>)(WithdrawalRefundFragment?)null
              );
            if (fragment.Status == RefundFragmentStatus.Settled)
              return Task.FromResult((Result<WithdrawalRefundFragment?>)fragment);
            return refundRepo.Update(
              fragment.Id,
              RefundFragmentStatus.Settled,
              refundId,
              DateTime.UtcNow
            );
          })
    );
    if (recorded.IsFailure())
      return recorded.FailureOrDefault();
    if (recorded.SuccessOrDefault() == null)
      return new StalePayoutEventException(
        $"settled event for unknown refund fragment '{requestId}' of withdrawal '{id}'"
      );

    // Step 2 (guarded transition): when ALL fragments of the current attempt
    // are settled, collect the reserve exactly once and complete — identical
    // ledger to the PayNow rail. Concurrent last-fragment webhooks are safe:
    // both may see all-settled, but the RepeatableRead write on the
    // withdrawal row aborts one of them.
    return await transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .ThenAwait(async w =>
          {
            var status = w.Principal.Status.Status;
            var payout = w.Principal.Payout;

            // idempotent redelivery: the withdrawal already completed
            if (status == WithdrawStatus.Completed)
              return (Result<WithdrawalPrincipal>)w.Principal;

            if (status != WithdrawStatus.Processing)
              return await Stale(
                $"settled refund event for fragment '{requestId}' but withdrawal '{id}' is '{status}'"
              );

            if (payout == null)
              return (Result<WithdrawalPrincipal>)
                new InvalidWithdrawalOperationException(
                  "Refund settlement requires an approved withdrawal with payout bookkeeping",
                  status,
                  WithdrawalOperations.SettleRefund
                );

            // an event for a superseded attempt must never settle the
            // current one — its refunds are not the money in flight now (the
            // evidence recorded above stands either way)
            if (attempt != null && attempt != payout.Attempt)
              return await Stale(
                $"settled refund event for attempt {attempt} of withdrawal '{id}', current attempt is {payout.Attempt}"
              );

            var allR = await refundRepo.ListByWithdrawal(id);
            if (allR.IsFailure())
              return (Result<WithdrawalPrincipal>)allR.FailureOrDefault();
            var prefix = $"{id}-{payout.Attempt}-";
            var siblings = allR
              .SuccessOrDefault()
              .Where(f => f.RequestId.StartsWith(prefix, StringComparison.Ordinal))
              .OrderBy(f => f.RequestId, StringComparer.Ordinal)
              .ToList();
            if (siblings.Count == 0)
              return await Stale(
                $"settled refund event for withdrawal '{id}' with no fragments for attempt {payout.Attempt}"
              );
            if (siblings.Any(f => f.Status != RefundFragmentStatus.Settled))
              // partial settlement: acknowledge and wait for the remaining
              // fragments' events
              return (Result<WithdrawalPrincipal>)w.Principal;

            var confirmation = payout.ConfirmationNumber ?? siblings[0].AirwallexRefundId!;
            return await CollectReserve(w, payout.Fee)
              .ThenAwait(_ =>
                repo.Update(
                    null,
                    id,
                    null,
                    new WithdrawalStatus { Status = WithdrawStatus.Completed },
                    new WithdrawalComplete
                    {
                      Note =
                        $"Automatically paid out via {siblings.Count} Airwallex card refund(s), first refund '{confirmation}'",
                      Receipt = null,
                      CompletedAt = DateTime.UtcNow,
                      CompleterId = null,
                    },
                    payout with
                    {
                      ConfirmationNumber = confirmation,
                    }
                  )
                  .NullToError(id.ToString())
              );
          })
    );
  }

  public async Task<Result<WithdrawalPrincipal>> FailRefundFragment(
    Guid id,
    string requestId,
    string refundId,
    string reason,
    int? attempt
  )
  {
    // Step 1 (own transaction): record the terminal failure. The fragment
    // releases its claim on the funding payment's refundable amount, and the
    // evidence survives any staleness fencing below.
    var recorded = await transactionManager.Start(
      () =>
        refundRepo
          .GetByRequestId(requestId)
          .ThenAwait(fragment =>
          {
            if (fragment == null)
              return Task.FromResult(
                (Result<WithdrawalRefundFragment?>)(WithdrawalRefundFragment?)null
              );
            if (fragment.Status == RefundFragmentStatus.Failed)
              return Task.FromResult((Result<WithdrawalRefundFragment?>)fragment);
            return refundRepo.Update(fragment.Id, RefundFragmentStatus.Failed, refundId, null);
          })
    );
    if (recorded.IsFailure())
      return recorded.FailureOrDefault();
    if (recorded.SuccessOrDefault() == null)
      return new StalePayoutEventException(
        $"failure event for unknown refund fragment '{requestId}' of withdrawal '{id}': {reason}"
      );

    // Step 2: park the withdrawal. Unlike a failed PayNow transfer, a failed
    // fragment can NOT bounce the withdrawal back to Pending: sibling
    // fragments may already be settled — money has partially left — and a
    // fresh attempt would refund it again. A human resolves it with the
    // fragment evidence showing exactly which refunds settled, failed, or
    // are still in flight.
    return await transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .ThenAwait(w =>
          {
            var status = w.Principal.Status.Status;
            var payout = w.Principal.Payout;

            // idempotent redelivery: an earlier failure event already parked it
            if (status == WithdrawStatus.RequireManualIntervention)
              return Task.FromResult((Result<WithdrawalPrincipal>)w.Principal);

            if (status != WithdrawStatus.Processing)
              return Stale($"refund failure event for withdrawal '{id}' in '{status}': {reason}");

            // a failure of a superseded attempt must not park the attempt
            // currently in flight
            if (payout != null && attempt != null && attempt != payout.Attempt)
              return Stale(
                $"refund failure event for attempt {attempt} of withdrawal '{id}', current attempt is {payout.Attempt}"
              );

            return repo.Update(
                null,
                id,
                null,
                new WithdrawalStatus { Status = WithdrawStatus.RequireManualIntervention },
                null,
                null
              )
              .NullToError(id.ToString());
          })
    );
  }

  public Task<Result<WithdrawalPrincipal>> CompletePayout(
    Guid id,
    string confirmationNumber,
    int? attempt,
    string? completerId = null
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

            // the webhook may only settle Processing; an admin force-complete
            // (completerId set) may also settle a parked withdrawal
            var allowed =
              status == WithdrawStatus.Processing
              || (completerId != null && status == WithdrawStatus.RequireManualIntervention);
            if (!allowed)
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
                        completerId == null
                          ? $"Automatically paid out via Airwallex transfer '{confirmationNumber}'"
                          : $"Force-completed against Airwallex transfer '{confirmationNumber}' after webhook loss",
                      Receipt = null,
                      CompletedAt = DateTime.UtcNow,
                      CompleterId = completerId,
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

  // Admin escape hatch for a confirmed Processing (or parked) withdrawal
  // whose settled webhook was permanently lost: after verifying the transfer
  // on the Airwallex dashboard, an admin finalizes it through the same
  // idempotent path the webhook would have taken (the transactional guard
  // still prevents any race with a late webhook).
  public Task<Result<WithdrawalPrincipal>> ForceCompletePayout(Guid id, string completerId)
  {
    return repo.Get(id, null)
      .NullToError(id.ToString())
      .ThenAwait(w =>
      {
        var status = w.Principal.Status.Status;
        var payout = w.Principal.Payout;
        var allowed =
          status is WithdrawStatus.Processing or WithdrawStatus.RequireManualIntervention;
        if (!allowed || payout?.ConfirmationNumber == null)
          return Task.FromResult(
            (Result<WithdrawalPrincipal>)
              new InvalidWithdrawalOperationException(
                "Force-completion requires a 'Processing' or 'RequireManualIntervention' withdrawal with a confirmed transfer",
                status,
                WithdrawalOperations.CompletePayout
              )
          );
        return this.CompletePayout(id, payout.ConfirmationNumber, payout.Attempt, completerId);
      });
  }

  // The number of inconclusive reconciliation sweeps tolerated before a
  // Processing withdrawal is parked for a human (8 sweeps at 6h ≈ 2 days)
  public const int MaxReconcileAttempts = 8;

  // Reconciliation: asks the gateway what actually happened to a Processing
  // withdrawal's payout — the PayNow transfer, or each still-pending refund
  // fragment of a card withdrawal. Settled/failed outcomes are delegated to
  // the same guarded transitions the webhooks use; anything inconclusive
  // (still in flight, not found, or the lookup itself failing) increments
  // the attempt counter and parks the withdrawal at the cap. Never moves
  // money itself.
  public async Task<Result<WithdrawalPrincipal>> Reconcile(Guid id)
  {
    var read = await repo.Get(id, null).NullToError(id.ToString());
    if (read.IsFailure())
      return read.FailureOrDefault();
    var w = read.SuccessOrDefault();
    var payout = w.Principal.Payout;
    if (w.Principal.Status.Status != WithdrawStatus.Processing || payout == null)
      return new InvalidWithdrawalOperationException(
        "Reconcile requires a 'Processing' withdrawal with payout bookkeeping",
        w.Principal.Status.Status,
        WithdrawalOperations.Reconcile
      );

    if (w.Principal.Record.Method == WithdrawalMethod.CardRefund)
    {
      var resolved = await this.ReconcileCardRefund(id, payout);
      if (resolved != null)
        return resolved.Value;
      // fall through: inconclusive, count the sweep below
    }
    else
    {
      var lookup = await payoutGateway.GetPayoutStatus(
        $"{id}-{payout.Attempt}",
        payout.ConfirmationNumber
      );

      if (lookup.IsSuccess())
      {
        var gateway = lookup.SuccessOrDefault();
        switch (gateway.Outcome)
        {
          case PayoutOutcome.Settled:
            return await this.CompletePayout(id, gateway.ConfirmationNumber!, payout.Attempt);
          case PayoutOutcome.Failed:
            return await this.FailPayout(
              id,
              "reconciliation: gateway reports the transfer terminally failed",
              payout.Attempt
            );
        }
      }
    }

    // Inconclusive: count the sweep; at the cap, park for a human. The write
    // is conditional on the state being unchanged so a concurrent webhook or
    // admin action is never clobbered.
    return await transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .ThenAwait(w2 =>
          {
            var p2 = w2.Principal.Payout;
            if (
              w2.Principal.Status.Status != WithdrawStatus.Processing
              || p2 == null
              || p2.Attempt != payout.Attempt
            )
              return Task.FromResult((Result<WithdrawalPrincipal>)w2.Principal);
            var attempts = p2.ReconcileAttempts + 1;
            return repo.Update(
                null,
                id,
                null,
                attempts >= MaxReconcileAttempts
                  ? new WithdrawalStatus { Status = WithdrawStatus.RequireManualIntervention }
                  : null,
                null,
                p2 with
                {
                  ReconcileAttempts = attempts,
                }
              )
              .NullToError(id.ToString());
          })
    );
  }

  // Card-refund reconciliation: poll each undecided fragment of the current
  // attempt at the gateway. Settlements/failures are delegated to the same
  // guarded webhook transitions (idempotent). Returns the resulting
  // principal when a decisive transition fired, or null when everything is
  // still inconclusive (caller counts the sweep).
  private async Task<Result<WithdrawalPrincipal>?> ReconcileCardRefund(
    Guid id,
    WithdrawalPayout payout
  )
  {
    var allR = await refundRepo.ListByWithdrawal(id);
    if (allR.IsFailure())
      return allR.FailureOrDefault();
    var prefix = $"{id}-{payout.Attempt}-";
    var fragments = allR
      .SuccessOrDefault()
      .Where(f => f.RequestId.StartsWith(prefix, StringComparison.Ordinal))
      .OrderBy(f => f.RequestId, StringComparer.Ordinal)
      .ToList();

    Result<WithdrawalPrincipal>? decisive = null;
    foreach (var fragment in fragments)
    {
      if (fragment.Status != RefundFragmentStatus.Created)
        continue;
      // a fragment without a refund id was never confirmed at the gateway;
      // the approve re-drive path owns it, not reconciliation
      if (fragment.AirwallexRefundId == null)
        continue;
      var lookup = await refundGateway.GetRefundStatus(fragment.AirwallexRefundId);
      if (lookup.IsFailure())
        continue;
      switch (lookup.SuccessOrDefault().Outcome)
      {
        case PayoutOutcome.Settled:
          decisive = await this.SettleRefundFragment(
            id,
            fragment.RequestId,
            fragment.AirwallexRefundId,
            payout.Attempt
          );
          break;
        case PayoutOutcome.Failed:
          // parking is terminal for this sweep: the remaining fragments'
          // evidence keeps flowing in via webhooks or later sweeps
          return await this.FailRefundFragment(
            id,
            fragment.RequestId,
            fragment.AirwallexRefundId,
            "reconciliation: gateway reports the refund terminally failed",
            payout.Attempt
          );
      }
    }

    return decisive;
  }

  // Admin only: return a parked withdrawal to Pending for another automated
  // attempt. The admin must have verified at the gateway that no live
  // transfer exists — the next approve mints a fresh request id, so
  // requeueing a withdrawal whose transfer is actually alive would double-pay.
  public Task<Result<WithdrawalPrincipal>> Requeue(Guid id)
  {
    return transactionManager.Start(
      () =>
        repo.Get(id, null)
          .NullToError(id.ToString())
          .DoAwait(
            DoType.MapErrors,
            w =>
              GuardStatus(
                w,
                WithdrawalOperations.Requeue,
                WithdrawStatus.RequireManualIntervention
              )
          )
          // Money safety for the card rail: a settled fragment means part of
          // the money has ALREADY reached the user's card, but the reserve
          // was never collected (that only happens when ALL fragments
          // settle). Requeueing would let the next approve re-plan the FULL
          // net against the remaining pool and pay the settled part twice.
          // Such withdrawals must be resolved manually (partial-settlement
          // bookkeeping is a human decision), never re-automated.
          .DoAwait(
            DoType.MapErrors,
            async w =>
            {
              if (w.Principal.Record.Method != WithdrawalMethod.CardRefund)
                return (Result<int>)0;
              var fragmentsR = await refundRepo.ListByWithdrawal(id);
              if (fragmentsR.IsFailure())
                return (Result<int>)fragmentsR.FailureOrDefault();
              if (
                fragmentsR
                  .SuccessOrDefault()
                  .Any(f => f.Status == RefundFragmentStatus.Settled)
              )
                return (Result<int>)
                  new InvalidWithdrawalOperationException(
                    "This card-refund withdrawal has settled refund fragments — money has partially reached the user's card, so it cannot be requeued for another automated attempt; resolve it manually",
                    w.Principal.Status.Status,
                    WithdrawalOperations.Requeue
                  );
              return (Result<int>)0;
            }
          )
          .ThenAwait(w =>
            repo.Update(
                null,
                id,
                null,
                new WithdrawalStatus { Status = WithdrawStatus.Pending },
                null,
                w.Principal.Payout == null
                  ? null
                  : w.Principal.Payout with
                  {
                    ConfirmationNumber = null,
                    ReconcileAttempts = 0,
                  }
              )
              .NullToError(id.ToString())
          )
    );
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

  public Task<Result<WithdrawalSettingsRecord>> GetCurrentSettings()
  {
    return settingsRepo
      .GetCurrent()
      .Then(s => s?.Record ?? WithdrawalSettingsRecord.Default, Errors.MapNone);
  }

  public Task<Result<WithdrawalSettingsPrincipal>> CreateSettings(
    WithdrawalSettingsRecord record
  )
  {
    return settingsRepo.Create(record);
  }

  // Collects the full reserved amount: net to BunnyBooker (paid out to the
  // user) and fee to the withdrawal-fee account, as two ledger transactions.
  // A disabled fee (0) books only the payout: a "SGD 0.00 fee charged" row
  // would contradict the fee being hidden everywhere else.
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
      .ThenAwait(t =>
        fee > 0
          ? transactionRepository.Create(
            w.Wallet.Id,
            generator.WithdrawalFeeCharge(w.Principal.Record, fee)
          )
          : Task.FromResult((Result<TransactionPrincipal>)t)
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
