using System.Text.Json;
using CSharp_Result;
using Domain;
using Domain.Payment;
using Domain.Transaction;
using Domain.Wallet;
using FluentAssertions;

namespace UnitTest.Payments;

// Deposit completion must credit the FULL captured amount, then collect the
// deposit fee (default zero — dormant until an admin queues one) out of
// Usable into the fee account as its own ledger row. A zero fee must produce
// NO fee ledger row and NO wallet collect.
public class PaymentServiceDepositFeeTests
{
  private const decimal Captured = 100m;

  private static (
    PaymentService Service,
    RecordingWalletRepository Wallet,
    RecordingTransactionRepository Txn
  ) Make(decimal percentage, decimal flat)
  {
    var wallet = new RecordingWalletRepository();
    var txn = new RecordingTransactionRepository();
    var service = new PaymentService(
      new FakePaymentRepository(),
      new UnusedPaymentGateway(),
      wallet,
      txn,
      new TransactionGenerator(new FixedRefundCalculator()),
      new PassThroughTransactionManager(),
      new App.Modules.Withdrawals.FeeCalculator(new FixedFeeRepository(percentage, flat))
    );
    return (service, wallet, txn);
  }

  [Fact]
  public async Task Zero_fee_deposits_full_amount_with_no_fee_row()
  {
    var (service, wallet, txn) = Make(percentage: 0m, flat: 0m);
    var r = await service.CompleteById(Guid.NewGuid(), Record());
    r.IsSuccess().Should().BeTrue();

    wallet.Deposited.Should().Be(Captured);
    wallet.Collected.Should().BeNull("a zero fee must not touch the wallet again");
    txn.Created.Should().ContainSingle().Which.Type.Should().Be(TransactionType.Deposit);
  }

  [Fact]
  public async Task Configured_fee_is_collected_as_its_own_ledger_row()
  {
    // 2% of $100 + $0.50 flat = $2.50
    var (service, wallet, txn) = Make(percentage: 2m, flat: 0.50m);
    var r = await service.CompleteById(Guid.NewGuid(), Record());
    r.IsSuccess().Should().BeTrue();

    wallet.Deposited.Should().Be(Captured, "the full captured amount is credited first");
    wallet.Collected.Should().Be(2.50m);
    txn.Created.Should().HaveCount(2);
    txn.Created[0].Type.Should().Be(TransactionType.Deposit);
    txn.Created[0].Amount.Should().Be(Captured);
    txn.Created[1].Type.Should().Be(TransactionType.DepositFee);
    txn.Created[1].Amount.Should().Be(2.50m);
    txn.Created[1].To.Should().Be(Accounts.DepositFee.DisplayName);
    // Transactions.PaymentId is UNIQUE and the Deposit row claims it — the
    // fee row must NOT reuse it or every fee-charging deposit rolls back
    txn.PaymentIds[0].Should().NotBeNull();
    txn.PaymentIds[1].Should().BeNull();
  }

  // ---- fixtures ----

  private static PaymentRecord Record() =>
    new()
    {
      Amount = Captured,
      CapturedAmount = Captured,
      Currency = "SGD",
      LastUpdated = DateTime.UtcNow,
      Status = "succeeded",
      AdditionalData = JsonDocument.Parse("{}"),
    };

  private static Payment PaymentWith(PaymentRecord record)
  {
    var walletId = Guid.NewGuid();
    return new Payment
    {
      Transaction = null,
      Principal = new PaymentPrincipal
      {
        Reference = new PaymentReference
        {
          Id = Guid.NewGuid(),
          ExternalReference = "ext-ref",
          Gateway = "Airwallex",
        },
        Record = record,
        CreatedAt = DateTime.UtcNow,
        Statuses = [],
      },
      Wallet = new WalletPrincipal
      {
        Id = walletId,
        UserId = "user-1",
        Record = new WalletRecord
        {
          Usable = 0m,
          WithdrawReserve = 0m,
          BookingReserve = 0m,
        },
      },
    };
  }

  // ---- fakes ----

  private sealed class FakePaymentRepository : IPaymentRepository
  {
    public Task<Result<IEnumerable<PaymentPrincipal>>> Search(PaymentSearch search) =>
      throw new NotSupportedException();

    public Task<Result<Payment?>> GetById(Guid id) => throw new NotSupportedException();

    public Task<Result<Payment?>> GetByRef(string id) => throw new NotSupportedException();

    public Task<Result<PaymentPrincipal>> Create(
      Guid walletId,
      PaymentReference r,
      PaymentRecord record
    ) => throw new NotSupportedException();

    public Task<Result<Payment?>> UpdateById(Guid id, PaymentRecord record) =>
      Task.FromResult<Result<Payment?>>(PaymentWith(record));

    public Task<Result<Payment?>> UpdateByRef(string reference, PaymentRecord record) =>
      Task.FromResult<Result<Payment?>>(PaymentWith(record));

    public Task<Result<Unit?>> DeleteById(Guid id) => throw new NotSupportedException();

    public Task<Result<Unit?>> DeleteByRef(string reference) => throw new NotSupportedException();
  }

  private sealed class UnusedPaymentGateway : IPaymentGateway
  {
    public Task<Result<(PaymentReference, PaymentRecord, PaymentSecret)>> Create(
      Guid id,
      decimal amount,
      string currency
    ) => throw new NotSupportedException();
  }

  private sealed class RecordingWalletRepository : IWalletRepository
  {
    public decimal? Deposited;
    public decimal? Collected;

    private static WalletPrincipal Principal(Guid id) =>
      new()
      {
        Id = id,
        UserId = "user-1",
        Record = new WalletRecord
        {
          Usable = 1000m,
          WithdrawReserve = 0m,
          BookingReserve = 0m,
        },
      };

    public Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount)
    {
      this.Deposited = amount;
      return Task.FromResult<Result<WalletPrincipal?>>(Principal(id));
    }

    public Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount)
    {
      this.Collected = amount;
      return Task.FromResult<Result<WalletPrincipal?>>(Principal(id));
    }

    public Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search) =>
      throw new NotSupportedException();

    public Task<Result<Wallet?>> Get(Guid id, string? userId) => throw new NotSupportedException();

    public Task<Result<Wallet?>> GetByUserId(string userId) => throw new NotSupportedException();

    public Task<Result<WalletPrincipal?>> PrepareWithdraw(Guid id, decimal amount) =>
      throw new NotSupportedException();

    public Task<Result<WalletPrincipal?>> Withdraw(Guid id, decimal amount) =>
      throw new NotSupportedException();

    public Task<Result<WalletPrincipal?>> CancelWithdraw(Guid id, decimal amount) =>
      throw new NotSupportedException();

    public Task<Result<WalletPrincipal?>> BookStart(Guid id, decimal amount) =>
      throw new NotSupportedException();

    public Task<Result<WalletPrincipal?>> BookEnd(Guid id, decimal revert, decimal collect) =>
      throw new NotSupportedException();
  }

  private sealed class RecordingTransactionRepository : ITransactionRepository
  {
    public readonly List<TransactionRecord> Created = [];
    public readonly List<Guid?> PaymentIds = [];

    public Task<Result<TransactionPrincipal>> Create(
      Guid walletId,
      TransactionRecord record,
      Guid? paymentId = null
    )
    {
      this.Created.Add(record);
      this.PaymentIds.Add(paymentId);
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
      throw new NotSupportedException();

    public Task<Result<Domain.Transaction.Transaction?>> Get(Guid id, string? userId) =>
      throw new NotSupportedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotSupportedException();
  }

  private sealed class FixedFeeRepository(decimal percentage, decimal flat) : IFeeRepository
  {
    public Task<Result<FeeChange?>> GetCurrent(FeeType type) =>
      Task.FromResult<Result<FeeChange?>>(
        type == FeeType.Deposit
          ? new FeeChange
          {
            Id = Guid.NewGuid(),
            Type = type,
            Percentage = percentage,
            FlatAmount = flat,
            EffectiveAt = DateTime.UtcNow.AddDays(-1),
          }
          : null
      );

    public Task<Result<IEnumerable<FeeChange>>> GetUpcoming(FeeType type) =>
      Task.FromResult<Result<IEnumerable<FeeChange>>>(Array.Empty<FeeChange>());

    public Task<Result<FeeChange>> Add(
      FeeType type,
      decimal percentage2,
      decimal flatAmount,
      DateTime? effectiveAt
    ) => throw new NotSupportedException();

    public Task<Result<FeeChange?>> CancelUpcoming(Guid id) => throw new NotSupportedException();
  }

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FixedRefundCalculator : IRefundCalculator
  {
    public decimal RefundRate => 0.5m;
    public decimal PenaltyRate => 0.5m;
  }
}
