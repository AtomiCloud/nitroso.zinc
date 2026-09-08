using CSharp_Result;
using Domain;
using Domain.Exceptions;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;
using Domain.Withdrawal;
using FluentAssertions;

namespace UnitTest.Withdrawals;

// The admin-configurable withdrawal method policy enforced at CREATE: the
// matrix of method x PayNowMode x pool coverage, the defaults when no
// settings row was ever written, and the settings read/write plumbing.
// The rails' money mechanics are covered by WithdrawalCardRefundTests and
// WithdrawalServiceGuardTests (both run with the policy wide open).
public class WithdrawalSettingsPolicyTests
{
  private const decimal Amount = 100m;

  private static readonly Guid WalletId = Guid.NewGuid();

  // ---- harness ----

  private sealed class Harness
  {
    public required WithdrawalService Service { get; init; }
    public required FakeWithdrawalSettingsRepository Settings { get; init; }
    public required FakeRefundRepository Refunds { get; init; }
    public required FakeWalletRepository Wallet { get; init; }
    public required FakeTransactionRepository Txn { get; init; }
  }

  private static Harness Make(WithdrawalSettingsRecord? settings)
  {
    var settingsRepo = new FakeWithdrawalSettingsRepository(settings);
    var refunds = new FakeRefundRepository();
    var wallet = new FakeWalletRepository();
    var txn = new FakeTransactionRepository();
    var service = new WithdrawalService(
      new FakeWithdrawalRepository(),
      wallet,
      txn,
      new TransactionGenerator(),
      new UnusedWithdrawalStorage(),
      new PassThroughTransactionManager(),
      new FourPercentFeeCalculator(),
      new UnusedPayoutGateway(),
      refunds,
      new UnusedRefundGateway(),
      settingsRepo
    );
    return new Harness
    {
      Service = service,
      Settings = settingsRepo,
      Refunds = refunds,
      Wallet = wallet,
      Txn = txn,
    };
  }

  private static WithdrawalSettingsRecord Settings(
    bool cardRefundEnabled = true,
    PayNowMode payNowMode = PayNowMode.Enabled,
    bool sweepEnabled = false
  ) =>
    new()
    {
      CardRefundEnabled = cardRefundEnabled,
      PayNowMode = payNowMode,
      SweepEnabled = sweepEnabled,
    };

  private static WithdrawalRecord Record(WithdrawalMethod method, decimal amount = Amount) =>
    new()
    {
      Amount = amount,
      Method = method,
      PayNowNumber = method == WithdrawalMethod.PayNow ? "91234567" : null,
    };

  private static FundingPayment Payment(decimal captured) =>
    new()
    {
      PaymentId = Guid.NewGuid(),
      PaymentIntentId = $"int_{Guid.NewGuid():N}",
      CreatedAt = DateTime.UtcNow.AddDays(-10),
      CapturedAmount = captured,
    };

  private static Task<Result<WithdrawalPrincipal>> Create(Harness h, WithdrawalRecord record) =>
    h.Service.Create("user-1", record);

  private static void ShouldBeRejectedCreate(Result<WithdrawalPrincipal> result, string fragment)
  {
    result.IsSuccess().Should().BeFalse();
    var e = result
      .FailureOrDefault()
      .Should()
      .BeOfType<InvalidWithdrawalOperationException>()
      .Which;
    e.Operation.Should().Be(WithdrawalOperations.Create);
    e.Message.Should().Contain(fragment);
  }

  // ---- defaults (no settings row) ----

  [Fact]
  public async Task Defaults_match_the_deployed_reality()
  {
    WithdrawalSettingsRecord.Default.CardRefundEnabled.Should().BeTrue();
    WithdrawalSettingsRecord.Default.PayNowMode.Should().Be(PayNowMode.FallbackOnly);
    WithdrawalSettingsRecord.Default.SweepEnabled.Should().BeFalse();

    var h = Make(null);
    var current = await h.Service.GetCurrentSettings();
    current.IsSuccess().Should().BeTrue();
    current.SuccessOrDefault().Should().Be(WithdrawalSettingsRecord.Default);
  }

  [Fact]
  public async Task No_row_card_refund_is_allowed()
  {
    var h = Make(null);
    h.Refunds.FundingPayments = [Payment(200m)];

    var result = await Create(h, Record(WithdrawalMethod.CardRefund));

    result.IsSuccess().Should().BeTrue();
  }

  [Fact]
  public async Task No_row_paynow_is_fallback_only()
  {
    var h = Make(null);
    // pool covers the amount, so under the default FallbackOnly PayNow is refused
    h.Refunds.FundingPayments = [Payment(200m)];

    var result = await Create(h, Record(WithdrawalMethod.PayNow));

    ShouldBeRejectedCreate(result, "fallback");
  }

  // ---- CardRefund x CardRefundEnabled ----

  [Fact]
  public async Task Card_refund_disabled_rejects_before_any_money_moves()
  {
    var h = Make(Settings(cardRefundEnabled: false));
    h.Refunds.FundingPayments = [Payment(200m)];

    var result = await Create(h, Record(WithdrawalMethod.CardRefund));

    ShouldBeRejectedCreate(result, "disabled");
    h.Wallet.PrepareWithdrawCalls.Should().Be(0);
    h.Txn.Records.Should().BeEmpty();
    h.Refunds.PoolQueries.Should().Be(0, "the policy rejects before the pool is computed");
  }

  [Fact]
  public async Task Card_refund_enabled_passes_the_policy()
  {
    var h = Make(Settings(cardRefundEnabled: true, payNowMode: PayNowMode.Disabled));
    h.Refunds.FundingPayments = [Payment(200m)];

    var result = await Create(h, Record(WithdrawalMethod.CardRefund));

    result.IsSuccess().Should().BeTrue();
    h.Wallet.PrepareWithdrawCalls.Should().Be(1);
  }

  [Fact]
  public async Task Card_refund_enabled_still_requires_a_covering_pool()
  {
    var h = Make(Settings(cardRefundEnabled: true));
    h.Refunds.FundingPayments = [Payment(10m)];

    var result = await Create(h, Record(WithdrawalMethod.CardRefund));

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InsufficientRefundablePoolException>();
  }

  // ---- PayNow x PayNowMode x pool ----

  [Theory]
  [InlineData(true)] // pool covers the amount
  [InlineData(false)] // pool does not
  public async Task Paynow_enabled_is_accepted_regardless_of_the_pool(bool poolCovers)
  {
    var h = Make(Settings(payNowMode: PayNowMode.Enabled));
    h.Refunds.FundingPayments = poolCovers ? [Payment(200m)] : [];

    var result = await Create(h, Record(WithdrawalMethod.PayNow));

    result.IsSuccess().Should().BeTrue();
    h.Refunds.PoolQueries.Should().Be(0, "Enabled never needs the pool");
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Paynow_disabled_is_rejected_regardless_of_the_pool(bool poolCovers)
  {
    var h = Make(Settings(payNowMode: PayNowMode.Disabled));
    h.Refunds.FundingPayments = poolCovers ? [Payment(200m)] : [];

    var result = await Create(h, Record(WithdrawalMethod.PayNow));

    ShouldBeRejectedCreate(result, "disabled");
    h.Wallet.PrepareWithdrawCalls.Should().Be(0);
    h.Txn.Records.Should().BeEmpty();
    h.Refunds.PoolQueries.Should().Be(0, "Disabled never needs the pool");
  }

  [Fact]
  public async Task Paynow_fallback_with_covering_pool_is_rejected_with_a_distinguishable_message()
  {
    var h = Make(Settings(payNowMode: PayNowMode.FallbackOnly));
    h.Refunds.FundingPayments = [Payment(Amount)];

    var result = await Create(h, Record(WithdrawalMethod.PayNow));

    ShouldBeRejectedCreate(result, "fallback");
    h.Wallet.PrepareWithdrawCalls.Should().Be(0);
    h.Refunds.PoolQueries.Should().Be(1, "the pool is computed exactly once");
  }

  [Fact]
  public async Task Paynow_fallback_with_short_pool_is_accepted()
  {
    var h = Make(Settings(payNowMode: PayNowMode.FallbackOnly));
    // pool < requested GROSS amount: the card rail cannot carry this
    // withdrawal, so PayNow is the legitimate fallback
    h.Refunds.FundingPayments = [Payment(Amount - 1m)];

    var result = await Create(h, Record(WithdrawalMethod.PayNow));

    result.IsSuccess().Should().BeTrue();
    h.Wallet.PrepareWithdrawCalls.Should().Be(1);
    h.Refunds.PoolQueries.Should().Be(1, "the pool is computed exactly once");
  }

  [Fact]
  public async Task Paynow_fallback_compares_the_pool_against_the_requested_amount()
  {
    var h = Make(Settings(payNowMode: PayNowMode.FallbackOnly));
    // pool exactly equals the requested amount -> card refunds can cover it
    h.Refunds.FundingPayments = [Payment(Amount)];

    var rejected = await Create(h, Record(WithdrawalMethod.PayNow));
    ShouldBeRejectedCreate(rejected, "fallback");
  }

  // ---- settings read/write plumbing ----

  [Fact]
  public async Task GetCurrentSettings_returns_the_newest_row()
  {
    var written = Settings(
      cardRefundEnabled: false,
      payNowMode: PayNowMode.Enabled,
      sweepEnabled: true
    );
    var h = Make(written);

    var current = await h.Service.GetCurrentSettings();

    current.IsSuccess().Should().BeTrue();
    current.SuccessOrDefault().Should().Be(written);
  }

  [Fact]
  public async Task CreateSettings_inserts_and_becomes_current()
  {
    var h = Make(null);
    var next = Settings(
      cardRefundEnabled: false,
      payNowMode: PayNowMode.Disabled,
      sweepEnabled: true
    );

    var created = await h.Service.CreateSettings(next);

    created.IsSuccess().Should().BeTrue();
    created.SuccessOrDefault().Record.Should().Be(next);
    h.Settings.Created.Should().ContainSingle().Which.Should().Be(next);
    (await h.Service.GetCurrentSettings()).SuccessOrDefault().Should().Be(next);
  }

  // ---- fakes (create-path only; everything else is never touched) ----

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FourPercentFeeCalculator : IFeeCalculator
  {
    public Task<Result<FeeSpec>> Current(FeeType type) =>
      Task.FromResult<Result<FeeSpec>>(new FeeSpec { Percentage = 4m, FlatAmount = 0m });

    public Task<Result<decimal>> Compute(FeeType type, decimal amount) =>
      Task.FromResult<Result<decimal>>(Math.Round(amount * 0.04m, 2, MidpointRounding.ToEven));
  }

  private sealed class FakeRefundRepository : IWithdrawalRefundRepository
  {
    public List<FundingPayment> FundingPayments { get; set; } = [];

    public int PoolQueries { get; private set; }

    public Task<Result<List<FundingPayment>>> ListFundingPayments(Guid walletId, DateTime since)
    {
      PoolQueries++;
      return Task.FromResult<Result<List<FundingPayment>>>(
        FundingPayments.Where(p => p.CreatedAt >= since).OrderBy(p => p.CreatedAt).ToList()
      );
    }

    public Task<Result<Dictionary<Guid, decimal>>> SumActiveRefundsByPayment(
      IEnumerable<Guid> paymentIds
    ) => Task.FromResult<Result<Dictionary<Guid, decimal>>>(new Dictionary<Guid, decimal>());

    public Task<Result<List<WithdrawalRefundFragment>>> ListByWithdrawal(Guid withdrawalId) =>
      throw new NotImplementedException();

    public Task<Result<WithdrawalRefundFragment?>> GetByRequestId(string requestId) =>
      throw new NotImplementedException();

    public Task<Result<List<WithdrawalRefundFragment>>> CreateMany(
      IEnumerable<WithdrawalRefundFragment> fragments
    ) => throw new NotImplementedException();

    public Task<Result<List<WithdrawalRefundFragment>>> ListSettledMissingArn(
      DateTime createdOnOrAfter,
      IEnumerable<Guid> excludeIds,
      int max
    ) => throw new NotImplementedException();

    public Task<Result<List<WithdrawalRefundFragment>>> ListByAirwallexRefundIds(
      IEnumerable<string> refundIds
    ) => throw new NotImplementedException();

    public Task<Result<List<PaymentIntentOwner>>> ListPaymentIntentOwners(
      IEnumerable<string> paymentIntentIds
    ) => throw new NotImplementedException();

    public Task<Result<List<WithdrawalCandidate>>> ListCandidatesByWallets(
      IEnumerable<Guid> walletIds
    ) => throw new NotImplementedException();

    public Task<Result<int>> CountUnbackfillableArn(DateTime createdBefore) =>
      throw new NotImplementedException();

    public Task<Result<WithdrawalRefundFragment?>> Update(
      Guid id,
      RefundFragmentStatus? status,
      string? airwallexRefundId,
      DateTime? settledAt,
      string? acquirerReferenceNumber
    ) => throw new NotImplementedException();
  }

  private sealed class FakeWithdrawalRepository : IWithdrawalRepository
  {
    public Task<Result<WithdrawalPrincipal>> Create(Guid walletId, WithdrawalRecord record) =>
      Task.FromResult<Result<WithdrawalPrincipal>>(
        new WithdrawalPrincipal
        {
          Id = Guid.NewGuid(),
          CreatedAt = DateTime.UtcNow,
          Status = new WithdrawalStatus { Status = WithdrawStatus.Pending },
          Record = record,
          Complete = null,
          Payout = null,
        }
      );

    public Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Withdrawal?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<WithdrawalPrincipal?>> Update(
      string? userId,
      Guid id,
      WithdrawalRecord? record,
      WithdrawalStatus? status,
      WithdrawalComplete? complete,
      WithdrawalPayout? payout = null
    ) => throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }

  private sealed class FakeWalletRepository : IWalletRepository
  {
    public int PrepareWithdrawCalls { get; private set; }

    private static WalletPrincipal Wallet() =>
      new()
      {
        Id = WalletId,
        UserId = "user-1",
        Record = new WalletRecord
        {
          Usable = 500m,
          WithdrawReserve = 0m,
          BookingReserve = 0m,
        },
      };

    public Task<Result<Wallet?>> GetByUserId(string userId) =>
      Task.FromResult<Result<Wallet?>>(
        new Wallet
        {
          Principal = Wallet(),
          User = new UserPrincipal
          {
            Id = "user-1",
            Record = new UserRecord { Username = "tester" },
          },
        }
      );

    public Task<Result<WalletPrincipal?>> PrepareWithdraw(Guid id, decimal amount)
    {
      PrepareWithdrawCalls++;
      return Task.FromResult<Result<WalletPrincipal?>>(Wallet());
    }

    public Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Wallet?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Withdraw(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> CancelWithdraw(Guid id, decimal amount) =>
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
      return Task.FromResult<Result<TransactionPrincipal>>(
        new TransactionPrincipal
        {
          Id = Guid.NewGuid(),
          CreatedAt = DateTime.UtcNow,
          Record = record,
        }
      );
    }

    public Task<Result<IEnumerable<TransactionPrincipal>>> Search(TransactionSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Transaction?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }

  private sealed class UnusedWithdrawalStorage : IWithdrawalStorage
  {
    public Task<Result<string>> Save(Stream stream) => throw new NotImplementedException();

    public Task<Result<string>> Get(string key) => throw new NotImplementedException();

    public Task<Result<string>> Get(string key, TimeSpan expiry) =>
      throw new NotImplementedException();
  }

  private sealed class UnusedPayoutGateway : IPayoutGateway
  {
    public Task<Result<PayoutConfirmation>> CreatePayout(PayoutRequest request) =>
      throw new NotImplementedException();

    public Task<Result<PayoutStatus>> GetPayoutStatus(
      string requestId,
      string? confirmationNumber
    ) => throw new NotImplementedException();
  }

  private sealed class UnusedRefundGateway : IRefundGateway
  {
    public Task<Result<RefundConfirmation>> CreateRefund(RefundRequest request) =>
      throw new NotImplementedException();

    public Task<Result<RefundStatus>> GetRefundStatus(string refundId) =>
      throw new NotImplementedException();

    public Task<Result<List<GatewayRefund>>> ListRefunds(DateTime fromUtc, DateTime toUtc) =>
      throw new NotImplementedException();
  }
}
