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

  private static (
    WithdrawalService Service,
    FakeWithdrawalRepository Repo,
    FakeWalletRepository Wallet,
    FakeTransactionRepository Txn,
    FakePayoutGateway Gateway
  ) Make(Withdrawal? withdrawal, bool gatewayFails = false)
  {
    var repo = new FakeWithdrawalRepository(withdrawal);
    var wallet = new FakeWalletRepository();
    var txn = new FakeTransactionRepository();
    var gateway = new FakePayoutGateway(gatewayFails);
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
  public async Task Approve_releases_claim_when_gateway_fails()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var (service, repo, wallet, _, gateway) = Make(w, gatewayFails: true);

    var result = await service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    gateway.Requests.Should().ContainSingle();
    repo.StatusWrites.Select(s => s.Status)
      .Should()
      .ContainInOrder(WithdrawStatus.Processing, WithdrawStatus.Pending);
    wallet.WithdrawCalls.Should().Be(0, "no money moves on approve, let alone on failure");
  }

  [Fact]
  public async Task Approve_retry_after_failure_uses_a_fresh_request_id()
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
      .Be($"{w.Principal.Id}-2", "each attempt must be unique at the gateway");
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

    var result = await service.CompletePayout(w.Principal.Id, "transfer-9");

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
    var w = WithdrawalWith(
      status,
      new WithdrawalPayout { ConfirmationNumber = "transfer-9", Fee = Fee, Attempt = 1 }
    );
    var (service, _, wallet, txn, _) = Make(w);

    var result = await service.CompletePayout(w.Principal.Id, "transfer-9");

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

    var result = await service.FailPayout(w.Principal.Id, "transfer failed");

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

    var result = await service.FailPayout(w.Principal.Id, "late failure event");

    result.IsSuccess().Should().BeFalse("a settled withdrawal must never be reopened");
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

  private sealed class FakePayoutGateway(bool fails) : IPayoutGateway
  {
    public List<PayoutRequest> Requests { get; } = [];

    public Task<Result<PayoutConfirmation>> CreatePayout(PayoutRequest request)
    {
      Requests.Add(request);
      if (fails)
        return Task.FromResult(
          (Result<PayoutConfirmation>)new InvalidOperationException("gateway down")
        );
      return Task.FromResult(
        (Result<PayoutConfirmation>)new PayoutConfirmation { Id = "transfer-1" }
      );
    }
  }

  private sealed class FakeWithdrawalRepository(Withdrawal? withdrawal) : IWithdrawalRepository
  {
    private WithdrawStatus? current;

    public List<WithdrawalStatus> StatusWrites { get; } = [];
    public WithdrawalPayout? LastPayoutWritten { get; private set; }
    public WithdrawalComplete? LastCompleteWritten { get; private set; }

    public Task<Result<Withdrawal?>> Get(Guid id, string? userId)
    {
      if (withdrawal == null)
        return Task.FromResult((Result<Withdrawal?>)(Withdrawal?)null);
      // reflect prior status writes, as the DB would inside the transaction
      var status =
        current == null
          ? withdrawal.Principal.Status
          : withdrawal.Principal.Status with
          {
            Status = current.Value,
          };
      var w = withdrawal with
      {
        Principal = withdrawal.Principal with { Status = status },
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
        LastPayoutWritten = payout;
      if (complete != null)
        LastCompleteWritten = complete;
      var principal = withdrawal!.Principal with
      {
        Status = status ?? withdrawal.Principal.Status,
        Payout = payout ?? withdrawal.Principal.Payout,
        Complete = complete ?? withdrawal.Principal.Complete,
      };
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
