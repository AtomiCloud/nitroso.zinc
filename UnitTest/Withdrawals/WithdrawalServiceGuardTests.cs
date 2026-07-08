using CSharp_Result;
using Domain;
using Domain.Exceptions;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;
using Domain.Withdrawal;
using FluentAssertions;

namespace UnitTest.Withdrawals;

// Guards + money movement on WithdrawalService for the automated payout flow.
// The invariants mirror the booking recovery spec: a withdrawal's reserve is
// collected at most once, a payout is created at most once per attempt, and
// the fee ledger always matches the transfer actually created.
public class WithdrawalServiceGuardTests
{
  private const decimal Amount = 100m;
  private const decimal Fee = 4m;

  private enum GatewayMode
  {
    Succeeds,
    RejectsDefinitively,
    FailsAmbiguously,
  }

  private static (
    WithdrawalService Service,
    FakeWithdrawalRepository Repo,
    FakeWalletRepository Wallet,
    FakeTransactionRepository Txn,
    FakePayoutGateway Gateway
  ) Make(Withdrawal? withdrawal, GatewayMode mode = GatewayMode.Succeeds)
  {
    var repo = new FakeWithdrawalRepository(withdrawal);
    var wallet = new FakeWalletRepository();
    var txn = new FakeTransactionRepository();
    var gateway = new FakePayoutGateway(mode);
    var service = new WithdrawalService(
      repo,
      wallet,
      txn,
      new TransactionGenerator(new FixedRefundCalculator()),
      new FakeWithdrawalStorage(),
      new PassThroughTransactionManager(),
      new FourPercentFeeCalculator(),
      gateway
    );
    return (service, repo, wallet, txn, gateway);
  }

  private static Withdrawal WithdrawalWith(
    WithdrawStatus status,
    WithdrawalPayout? payout = null
  )
  {
    var id = Guid.NewGuid();
    return new Withdrawal
    {
      Principal = new WithdrawalPrincipal
      {
        Id = id,
        CreatedAt = DateTime.UtcNow,
        Status = new WithdrawalStatus { Status = status },
        Record = new WithdrawalRecord { Amount = Amount, PayNowNumber = "91234567" },
        Complete = null,
        Payout = payout,
      },
      Wallet = new WalletPrincipal
      {
        Id = Guid.NewGuid(),
        UserId = "user-1",
        Record = new WalletRecord
        {
          Usable = 0m,
          WithdrawReserve = Amount,
          BookingReserve = 0m,
        },
      },
      User = new UserPrincipal
      {
        Id = "user-1",
        Record = new UserRecord { Username = "tester" },
      },
      Completer = null,
    };
  }

  // ---- Approve ----

  [Fact]
  public async Task Approve_pending_claims_processing_and_pays_out_net_amount()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var (service, repo, _, _, gateway) = Make(w);

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    repo.StatusWrites.Should().Contain(s => s.Status == WithdrawStatus.Processing);
    gateway.Requests.Should().ContainSingle();
    gateway.Requests[0].Amount.Should().Be(Amount - Fee, "the payout is net of the 4% fee");
    gateway.Requests[0].RequestId.Should().Be($"{w.Principal.Id}-1");
    repo.LastPayoutWritten!.ConfirmationNumber.Should().Be("transfer-1");
    repo.LastPayoutWritten.Fee.Should().Be(Fee);
  }

  [Theory]
  [InlineData(WithdrawStatus.Processing)]
  [InlineData(WithdrawStatus.Completed)]
  [InlineData(WithdrawStatus.Rejected)]
  [InlineData(WithdrawStatus.Cancel)]
  public async Task Approve_from_disallowed_status_is_rejected_and_never_hits_gateway(
    WithdrawStatus status
  )
  {
    var w = WithdrawalWith(status);
    var (service, repo, _, _, gateway) = Make(w);

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeFalse($"a '{status}' withdrawal must not be approved");
    result.FailureOrDefault().Should().BeOfType<InvalidWithdrawalOperationException>();
    gateway.Requests.Should().BeEmpty("no payout may be created when the claim guard fails");
    repo.StatusWrites.Should().BeEmpty();
  }

  [Fact]
  public async Task Approve_releases_claim_only_on_definitive_gateway_rejection()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var (service, repo, wallet, _, gateway) = Make(w, GatewayMode.RejectsDefinitively);

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    gateway.Requests.Should().ContainSingle();
    repo.StatusWrites.Select(s => s.Status)
      .Should()
      .ContainInOrder(WithdrawStatus.Processing, WithdrawStatus.Pending);
    wallet.WithdrawCalls.Should().Be(0, "no money moves on approve, let alone on failure");
  }

  [Fact]
  public async Task Approve_keeps_claim_on_ambiguous_gateway_failure()
  {
    // A timeout/5xx does not prove the transfer was not created: releasing
    // the claim here would let a retry mint a fresh request id and pay twice
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var (service, repo, wallet, _, gateway) = Make(w, GatewayMode.FailsAmbiguously);

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    gateway.Requests.Should().ContainSingle();
    repo.StatusWrites.Select(s => s.Status)
      .Should()
      .Equal([WithdrawStatus.Processing], "the withdrawal must stay claimed in Processing");
    wallet.WithdrawCalls.Should().Be(0);
  }

  [Fact]
  public async Task Approve_redrives_processing_without_confirmation_reusing_the_request_id()
  {
    // After an ambiguous failure the withdrawal sits in Processing with no
    // confirmation number; a re-drive must reuse the SAME request id so the
    // gateway's idempotency collapses it into the original transfer
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = null, Fee = Fee, Attempt = 1 }
    );
    var (service, repo, _, _, gateway) = Make(w);

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    gateway.Requests[0].RequestId.Should().Be($"{w.Principal.Id}-1", "the attempt is reused");
    repo.LastPayoutWritten!.Attempt.Should().Be(1);
    repo.LastPayoutWritten.ConfirmationNumber.Should().Be("transfer-1");
  }

  [Fact]
  public async Task Approve_redrive_failure_never_releases_the_claim()
  {
    // On a re-drive even a definitive-looking 4xx may just be the gateway
    // deduplicating the original request id — the claim must survive
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = null, Fee = Fee, Attempt = 1 }
    );
    var (service, repo, _, _, _) = Make(w, GatewayMode.RejectsDefinitively);

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    repo.StatusWrites.Select(s => s.Status)
      .Should()
      .Equal([WithdrawStatus.Processing], "no rollback to Pending on a re-drive");
  }

  [Fact]
  public async Task Approve_of_processing_with_confirmation_is_rejected()
  {
    // A confirmed transfer is in flight; approving again is meaningless and
    // must not touch the gateway
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = "transfer-1", Fee = Fee, Attempt = 1 }
    );
    var (service, _, _, _, gateway) = Make(w);

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidWithdrawalOperationException>();
    gateway.Requests.Should().BeEmpty();
  }

  [Fact]
  public async Task Approve_retry_after_definitive_rejection_uses_a_fresh_request_id()
  {
    var w = WithdrawalWith(
      WithdrawStatus.Pending,
      new WithdrawalPayout { ConfirmationNumber = null, Fee = Fee, Attempt = 1 }
    );
    var (service, _, _, _, gateway) = Make(w);

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    gateway.Requests[0].RequestId
      .Should()
      .Be($"{w.Principal.Id}-2", "a rejected attempt's request id is burned");
  }

  [Fact]
  public async Task Approve_phase3_never_clobbers_a_concurrent_transition()
  {
    // While the gateway call is in flight, a fast transfer.failed webhook
    // returns the withdrawal to Pending and clears the dead confirmation.
    // Phase 3 must NOT resurrect it by writing its stale snapshot.
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var (service, repo, _, _, _) = Make(w);
    repo.AfterFirstUpdate = r =>
      r.MutateState(
        WithdrawStatus.Pending,
        new WithdrawalPayout { ConfirmationNumber = null, Fee = Fee, Attempt = 1 }
      );

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeTrue("the withdrawal simply moved on; nothing to report");
    repo.LastPayoutWritten!.ConfirmationNumber
      .Should()
      .BeNull("the stale snapshot must not overwrite the concurrent transition");
  }

  // ---- CompletePayout (webhook success) ----

  [Fact]
  public async Task CompletePayout_processing_collects_reserve_and_writes_two_ledger_rows()
  {
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = "transfer-9", Fee = Fee, Attempt = 1 }
    );
    var (service, repo, wallet, txn, _) = Make(w);

    var result = await service.CompletePayout(w.Principal.Id, "transfer-9", 1);

    result.IsSuccess().Should().BeTrue();
    wallet.WithdrawCalls.Should().Be(1);
    wallet.LastWithdrawAmount.Should().Be(Amount, "the full reserved amount is collected");
    txn.Records.Should().HaveCount(2);
    txn.Records[0].Type.Should().Be(TransactionType.WithdrawComplete);
    txn.Records[0].Amount.Should().Be(Amount - Fee);
    txn.Records[1].Type.Should().Be(TransactionType.WithdrawFee);
    txn.Records[1].Amount.Should().Be(Fee);
    txn.Records[1].To.Should().Be(Accounts.WithdrawalFee.DisplayName);
    repo.StatusWrites.Should().ContainSingle(s => s.Status == WithdrawStatus.Completed);
    repo.LastCompleteWritten!.CompleterId.Should().BeNull("automation has no human completer");
  }

  [Theory]
  [InlineData(WithdrawStatus.Pending)]
  [InlineData(WithdrawStatus.Completed)]
  [InlineData(WithdrawStatus.Rejected)]
  [InlineData(WithdrawStatus.Cancel)]
  public async Task CompletePayout_from_disallowed_status_moves_no_money(WithdrawStatus status)
  {
    // stored confirmation differs from the event's transfer, so even the
    // Completed case is NOT the idempotent-redelivery path (covered below)
    var w = WithdrawalWith(
      status,
      new WithdrawalPayout { ConfirmationNumber = "transfer-orig", Fee = Fee, Attempt = 1 }
    );
    var (service, _, wallet, txn, _) = Make(w);

    var result = await service.CompletePayout(w.Principal.Id, "transfer-9", 1);

    result.IsSuccess().Should().BeFalse(
      $"a '{status}' withdrawal must not be completed by the webhook (double-collect protection)"
    );
    wallet.WithdrawCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
  }

  // ---- FailPayout (webhook failure) ----

  [Fact]
  public async Task FailPayout_processing_returns_to_pending_without_money()
  {
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = "transfer-9", Fee = Fee, Attempt = 1 }
    );
    var (service, repo, wallet, txn, _) = Make(w);

    var result = await service.FailPayout(w.Principal.Id, "transfer failed", 1);

    result.IsSuccess().Should().BeTrue();
    repo.StatusWrites.Should().ContainSingle(s => s.Status == WithdrawStatus.Pending);
    wallet.WithdrawCalls.Should().Be(0);
    wallet.CancelWithdrawCalls.Should().Be(0, "the reserve was never collected, nothing to refund");
    txn.Records.Should().BeEmpty();
  }

  [Fact]
  public async Task FailPayout_from_completed_is_rejected()
  {
    var w = WithdrawalWith(
      WithdrawStatus.Completed,
      new WithdrawalPayout { ConfirmationNumber = "transfer-9", Fee = Fee, Attempt = 1 }
    );
    var (service, repo, _, _, _) = Make(w);

    var result = await service.FailPayout(w.Principal.Id, "late failure event", 1);

    result.IsSuccess().Should().BeFalse("a settled withdrawal must never be reopened");
    repo.StatusWrites.Should().BeEmpty();
  }

  [Fact]
  public async Task FailPayout_clears_the_dead_transfers_confirmation_number()
  {
    // a failed transfer's id must never survive as "proof of payment" for a
    // later manual completion
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = "transfer-dead", Fee = Fee, Attempt = 1 }
    );
    var (service, repo, _, _, _) = Make(w);

    var result = await service.FailPayout(w.Principal.Id, "transfer failed", 1);

    result.IsSuccess().Should().BeTrue();
    repo.LastPayoutWritten!.ConfirmationNumber.Should().BeNull();
    repo.LastPayoutWritten.Attempt.Should().Be(1, "the attempt counter must survive for uniqueness");
  }

  // ---- ForceCompletePayout (admin escape hatch) ----

  [Fact]
  public async Task ForceComplete_finalizes_a_confirmed_processing_withdrawal()
  {
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = "transfer-9", Fee = Fee, Attempt = 1 }
    );
    var (service, repo, wallet, txn, _) = Make(w);

    var result = await service.ForceCompletePayout(w.Principal.Id, "admin-1");

    result.IsSuccess().Should().BeTrue();
    wallet.LastWithdrawAmount.Should().Be(Amount);
    txn.Records.Should().HaveCount(2);
    repo.StatusWrites.Should().ContainSingle(s => s.Status == WithdrawStatus.Completed);
    repo.LastCompleteWritten!.CompleterId
      .Should()
      .Be("admin-1", "the forcing admin is recorded for the audit trail");
  }

  [Fact]
  public async Task ForceComplete_of_unconfirmed_processing_is_rejected()
  {
    // without a confirmation number there is no verified transfer to settle
    // against — the re-drive path handles these instead
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = null, Fee = Fee, Attempt = 1 }
    );
    var (service, _, wallet, txn, _) = Make(w);

    var result = await service.ForceCompletePayout(w.Principal.Id, "admin-1");

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidWithdrawalOperationException>();
    wallet.WithdrawCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
  }

  [Fact]
  public async Task ForceComplete_of_pending_is_rejected()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var (service, _, wallet, _, _) = Make(w);

    var result = await service.ForceCompletePayout(w.Principal.Id, "admin-1");

    result.IsSuccess().Should().BeFalse();
    wallet.WithdrawCalls.Should().Be(0);
  }

  // ---- Webhook idempotency and attempt fencing ----

  [Fact]
  public async Task CompletePayout_redelivered_settled_event_acks_without_moving_money()
  {
    var w = WithdrawalWith(
      WithdrawStatus.Completed,
      new WithdrawalPayout { ConfirmationNumber = "transfer-9", Fee = Fee, Attempt = 1 }
    );
    var (service, repo, wallet, txn, _) = Make(w);

    var result = await service.CompletePayout(w.Principal.Id, "transfer-9", 1);

    result.IsSuccess().Should().BeTrue("gateways redeliver webhooks at least once");
    wallet.WithdrawCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
    repo.StatusWrites.Should().BeEmpty();
  }

  [Fact]
  public async Task CompletePayout_for_superseded_attempt_is_stale_and_moves_no_money()
  {
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = null, Fee = Fee, Attempt = 2 }
    );
    var (service, _, wallet, txn, _) = Make(w);

    var result = await service.CompletePayout(w.Principal.Id, "transfer-old", 1);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<StalePayoutEventException>();
    wallet.WithdrawCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
  }

  [Fact]
  public async Task FailPayout_redelivered_failure_event_acks_idempotently()
  {
    var w = WithdrawalWith(
      WithdrawStatus.Pending,
      new WithdrawalPayout { ConfirmationNumber = null, Fee = Fee, Attempt = 1 }
    );
    var (service, repo, _, _, _) = Make(w);

    var result = await service.FailPayout(w.Principal.Id, "redelivered failure", 1);

    result.IsSuccess().Should().BeTrue();
    repo.StatusWrites.Should().BeEmpty();
  }

  [Fact]
  public async Task FailPayout_for_superseded_attempt_never_releases_the_live_claim()
  {
    // a late failure event for attempt 1 must not knock attempt 2's live
    // claim back to Pending — that reopens the double-payout window
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = "transfer-2", Fee = Fee, Attempt = 2 }
    );
    var (service, repo, _, _, _) = Make(w);

    var result = await service.FailPayout(w.Principal.Id, "late failure for attempt 1", 1);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<StalePayoutEventException>();
    repo.StatusWrites.Should().BeEmpty();
  }

  // ---- Manual flows keep their guards ----

  [Theory]
  [InlineData(WithdrawStatus.Processing)]
  [InlineData(WithdrawStatus.Completed)]
  [InlineData(WithdrawStatus.Rejected)]
  [InlineData(WithdrawStatus.Cancel)]
  public async Task Cancel_from_non_pending_is_rejected_and_refunds_nothing(WithdrawStatus status)
  {
    var w = WithdrawalWith(status);
    var (service, _, wallet, txn, _) = Make(w);

    var result = await service.Cancel(w.Principal.Id, "user-1", "changed my mind");

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidWithdrawalOperationException>();
    wallet.CancelWithdrawCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
  }

  [Theory]
  [InlineData(WithdrawStatus.Processing)]
  [InlineData(WithdrawStatus.Completed)]
  public async Task Reject_from_disallowed_status_is_rejected_and_refunds_nothing(
    WithdrawStatus status
  )
  {
    var w = WithdrawalWith(status);
    var (service, _, wallet, txn, _) = Make(w);

    var result = await service.Reject(w.Principal.Id, "admin-1", "suspicious");

    result.IsSuccess().Should().BeFalse();
    wallet.CancelWithdrawCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
  }

  [Fact]
  public async Task Manual_complete_charges_the_same_fee_ledger()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var (service, repo, wallet, txn, _) = Make(w);

    var result = await service.Complete(
      w.Principal.Id,
      "admin-1",
      "manual PayNow transfer",
      new MemoryStream([1, 2, 3])
    );

    result.IsSuccess().Should().BeTrue();
    wallet.LastWithdrawAmount.Should().Be(Amount);
    txn.Records.Should().HaveCount(2);
    txn.Records[0].Amount.Should().Be(Amount - Fee);
    txn.Records[1].Amount.Should().Be(Fee);
    repo.LastPayoutWritten!.Fee.Should().Be(Fee);
    repo.LastCompleteWritten!.CompleterId.Should().Be("admin-1");
  }

  [Fact]
  public async Task Manual_complete_from_processing_is_rejected()
  {
    // A Processing withdrawal has a live transfer at the gateway; a manual
    // completion on top of it would pay the user twice
    var w = WithdrawalWith(
      WithdrawStatus.Processing,
      new WithdrawalPayout { ConfirmationNumber = "transfer-9", Fee = Fee, Attempt = 1 }
    );
    var (service, _, wallet, txn, _) = Make(w);

    var result = await service.Complete(
      w.Principal.Id,
      "admin-1",
      "manual",
      new MemoryStream([1])
    );

    result.IsSuccess().Should().BeFalse();
    wallet.WithdrawCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
  }

  // ---- fakes ----

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FourPercentFeeCalculator : IFeeCalculator
  {
    public decimal WithdrawFeeRate => 0.04m;

    public decimal WithdrawFee(decimal amount) =>
      Math.Round(amount * this.WithdrawFeeRate, 2, MidpointRounding.ToEven);
  }

  private sealed class FixedRefundCalculator : IRefundCalculator
  {
    public decimal RefundRate => 0.5m;
    public decimal PenaltyRate => 0.5m;
  }

  private sealed class FakeWithdrawalStorage : IWithdrawalStorage
  {
    public Task<Result<string>> Save(Stream stream) =>
      Task.FromResult((Result<string>)"receipt-key");

    public Task<Result<string>> Get(string key) => Task.FromResult((Result<string>)"url");
  }

  private sealed class FakePayoutGateway(GatewayMode mode) : IPayoutGateway
  {
    public List<PayoutRequest> Requests { get; } = [];

    public Task<Result<PayoutConfirmation>> CreatePayout(PayoutRequest request)
    {
      Requests.Add(request);
      return Task.FromResult<Result<PayoutConfirmation>>(
        mode switch
        {
          GatewayMode.RejectsDefinitively => new PayoutRejectedException("invalid beneficiary"),
          GatewayMode.FailsAmbiguously => new HttpRequestException("gateway timeout"),
          _ => new PayoutConfirmation { Id = "transfer-1" },
        }
      );
    }
  }

  private sealed class FakeWithdrawalRepository(Withdrawal? withdrawal) : IWithdrawalRepository
  {
    private WithdrawStatus? current;
    private WithdrawalPayout? currentPayout = withdrawal?.Principal.Payout;
    private bool payoutTouched;

    public List<WithdrawalStatus> StatusWrites { get; } = [];
    public WithdrawalPayout? LastPayoutWritten { get; private set; }
    public WithdrawalComplete? LastCompleteWritten { get; private set; }

    // simulates a concurrent, committed transition (e.g. a webhook) landing
    // between the caller's transactions
    public Action<FakeWithdrawalRepository>? AfterFirstUpdate { get; set; }

    public void MutateState(WithdrawStatus status, WithdrawalPayout? payout)
    {
      current = status;
      currentPayout = payout;
      payoutTouched = true;
    }

    public Task<Result<Withdrawal?>> Get(Guid id, string? userId)
    {
      if (withdrawal == null)
        return Task.FromResult((Result<Withdrawal?>)(Withdrawal?)null);
      // reflect prior status/payout writes, as the DB would
      var status =
        current == null
          ? withdrawal.Principal.Status
          : withdrawal.Principal.Status with
          {
            Status = current.Value,
          };
      var w = withdrawal with
      {
        Principal = withdrawal.Principal with
        {
          Status = status,
          Payout = payoutTouched ? currentPayout : withdrawal.Principal.Payout,
        },
      };
      return Task.FromResult((Result<Withdrawal?>)w);
    }

    public Task<Result<WithdrawalPrincipal?>> Update(
      string? userId,
      Guid id,
      WithdrawalRecord? record,
      WithdrawalStatus? status,
      WithdrawalComplete? complete,
      WithdrawalPayout? payout = null
    )
    {
      if (status != null)
      {
        StatusWrites.Add(status);
        current = status.Status;
      }
      if (payout != null)
      {
        LastPayoutWritten = payout;
        currentPayout = payout;
        payoutTouched = true;
      }
      if (complete != null)
        LastCompleteWritten = complete;
      var principal = withdrawal!.Principal with
      {
        Status = status ?? withdrawal.Principal.Status,
        Payout = payout ?? withdrawal.Principal.Payout,
        Complete = complete ?? withdrawal.Principal.Complete,
      };

      var hook = AfterFirstUpdate;
      AfterFirstUpdate = null;
      hook?.Invoke(this);

      return Task.FromResult((Result<WithdrawalPrincipal?>)principal);
    }

    public Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search) =>
      throw new NotImplementedException();

    public Task<Result<WithdrawalPrincipal>> Create(Guid walletId, WithdrawalRecord record) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }

  private sealed class FakeWalletRepository : IWalletRepository
  {
    public int WithdrawCalls { get; private set; }
    public int CancelWithdrawCalls { get; private set; }
    public decimal? LastWithdrawAmount { get; private set; }

    private static WalletPrincipal Wallet(Guid id) =>
      new()
      {
        Id = id,
        UserId = "user-1",
        Record = new WalletRecord
        {
          Usable = 0m,
          WithdrawReserve = 0m,
          BookingReserve = 0m,
        },
      };

    public Task<Result<WalletPrincipal?>> Withdraw(Guid id, decimal amount)
    {
      WithdrawCalls++;
      LastWithdrawAmount = amount;
      return Task.FromResult((Result<WalletPrincipal?>)Wallet(id));
    }

    public Task<Result<WalletPrincipal?>> CancelWithdraw(Guid id, decimal amount)
    {
      CancelWithdrawCalls++;
      return Task.FromResult((Result<WalletPrincipal?>)Wallet(id));
    }

    public Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Wallet?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<Wallet?>> GetByUserId(string userId) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> PrepareWithdraw(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> BookStart(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> BookEnd(Guid id, decimal revert, decimal collect) =>
      throw new NotImplementedException();
  }

  private sealed class FakeTransactionRepository : ITransactionRepository
  {
    public List<TransactionRecord> Records { get; } = [];

    public Task<Result<TransactionPrincipal>> Create(
      Guid walletId,
      TransactionRecord record,
      Guid? paymentId = null
    )
    {
      Records.Add(record);
      var principal = new TransactionPrincipal
      {
        Id = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        Record = record,
      };
      return Task.FromResult((Result<TransactionPrincipal>)principal);
    }

    public Task<Result<IEnumerable<TransactionPrincipal>>> Search(TransactionSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Transaction?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }
}
