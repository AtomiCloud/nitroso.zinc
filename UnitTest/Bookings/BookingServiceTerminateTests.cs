using App.Modules.Withdrawals;
using CSharp_Result;
using Domain;
using Domain.Booking;
using Domain.Passenger;
using Domain.Timings;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Bookings;

// Terminate settles through the fee engine: fee = Compute(Termination,
// amount), refund = amount - fee, computed ONCE — the wallet deposit and the
// ledger row must always carry the same numbers. The migration seeds
// { Termination, 50%, 0 flat, no cap } for parity with the old
// Amount * RefundRate behavior.
public class BookingServiceTerminateTests
{
  private const decimal Cost = 16m;

  // exercises the REAL App FeeCalculator so cap/rounding semantics are the
  // ones production uses
  private static (
    BookingService Service,
    FakeWalletRepository Wallet,
    FakeTransactionRepository Txn
  ) Make(Booking booking, decimal? percentage = null, decimal? flat = null, decimal? cap = null)
  {
    var wallet = new FakeWalletRepository();
    var txn = new FakeTransactionRepository();
    var service = new BookingService(
      new FakeBookingRepository(booking),
      null!,
      wallet,
      txn,
      new PassThroughTransactionManager(),
      new TransactionGenerator(),
      new FeeCalculator(new FixedTerminationFeeRepository(percentage, flat, cap)),
      new FakeTerminator(),
      new FakeCdc(),
      new FakeNotifier(),
      null!,
      null!,
      NullLogger<BookingService>.Instance
    );
    return (service, wallet, txn);
  }

  private static Booking CompletedBooking()
  {
    var id = Guid.NewGuid();
    return new Booking
    {
      Principal = new BookingPrincipal
      {
        Id = id,
        UserId = "user-1",
        CreatedAt = DateTime.UtcNow,
        Record = new BookingRecord
        {
          Date = new DateOnly(2026, 7, 15),
          Time = new TimeOnly(8, 30),
          Direction = TrainDirection.JToW,
          Passenger = new PassengerRecord
          {
            FullName = "Test Passenger",
            Gender = PassengerGender.M,
            PassportExpiry = new DateOnly(2030, 1, 1),
            PassportNumber = "P1234567",
          },
        },
        Status = new BookingStatus { Status = BookStatus.Completed, CompletedAt = null },
        Complete = new BookingComplete
        {
          Ticket = null,
          BookingNumber = "BN-1",
          TicketNumber = "TN-1",
        },
        Priority = false,
        PriorityFee = null,
      },
      User = new UserPrincipal
      {
        Id = "user-1",
        Record = new UserRecord { Username = "tester" },
      },
      Transaction = new TransactionPrincipal
      {
        Id = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        Record = new TransactionRecord
        {
          Name = "Purchased Booking Service",
          Description = "test",
          Type = TransactionType.BookingRequest,
          Amount = Cost,
          From = Accounts.Usable.DisplayName,
          To = Accounts.BookingReserve.DisplayName,
        },
      },
      Wallet = new WalletPrincipal
      {
        Id = Guid.NewGuid(),
        UserId = "user-1",
        Record = new WalletRecord
        {
          Usable = 100m,
          WithdrawReserve = 0m,
          BookingReserve = Cost,
        },
      },
    };
  }

  // before the 2026-07-15 08:30 SGT departure
  private static readonly DateTime BeforeDeparture = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

  [Fact]
  public async Task Fifty_percent_seed_keeps_parity_with_the_old_refund_rate()
  {
    var b = CompletedBooking();
    var (service, wallet, txn) = Make(b, percentage: 50m);

    var result = await service.Terminate("user-1", b.Principal.Id, BeforeDeparture);

    result.IsSuccess().Should().BeTrue();
    wallet.DepositCalls.Should().Be(1);
    wallet.LastDepositAmount.Should().Be(8m, "fee = 50% of 16 = 8, refund = 16 - 8 = 8");
    var record = txn.Records.Single(r => r.Type == TransactionType.BookingTerminated);
    record.Amount.Should().Be(8m, "the ledger moves exactly the refund");
    record.Description.Should().Contain("SGD 8.00", "refund and fee both render as 8.00");
  }

  [Fact]
  public async Task Cap_binds_the_termination_fee_and_grows_the_refund()
  {
    var b = CompletedBooking();
    var (service, wallet, txn) = Make(b, percentage: 50m, cap: 5m);

    var result = await service.Terminate("user-1", b.Principal.Id, BeforeDeparture);

    result.IsSuccess().Should().BeTrue();
    wallet.LastDepositAmount.Should().Be(11m, "fee = min(8, cap 5) = 5, refund = 16 - 5 = 11");
    var record = txn.Records.Single(r => r.Type == TransactionType.BookingTerminated);
    record.Amount.Should().Be(11m);
    record.Description.Should().Contain("SGD 11.00", "the actual refund renders");
    record.Description.Should().Contain("SGD 5.00", "the actual (capped) fee renders");
  }

  [Fact]
  public async Task Flat_plus_percentage_plus_cap_combine_in_the_settlement()
  {
    var b = CompletedBooking();
    // 25% of 16 = 4, + 1 flat = 5, capped at 3
    var (service, wallet, txn) = Make(b, percentage: 25m, flat: 1m, cap: 3m);

    var result = await service.Terminate("user-1", b.Principal.Id, BeforeDeparture);

    result.IsSuccess().Should().BeTrue();
    wallet.LastDepositAmount.Should().Be(13m, "refund = 16 - min(5, 3)");
    txn.Records.Single(r => r.Type == TransactionType.BookingTerminated).Amount.Should().Be(13m);
  }

  [Fact]
  public async Task No_fee_row_refunds_the_full_amount()
  {
    var b = CompletedBooking();
    var (service, wallet, txn) = Make(b);

    var result = await service.Terminate("user-1", b.Principal.Id, BeforeDeparture);

    result.IsSuccess().Should().BeTrue();
    wallet.LastDepositAmount.Should().Be(Cost, "zero-zero fee = free termination, full refund");
    var record = txn.Records.Single(r => r.Type == TransactionType.BookingTerminated);
    record.Amount.Should().Be(Cost);
    record.Description.Should().Contain("SGD 16.00");
    record.Description.Should().Contain("SGD 0.00");
  }

  [Fact]
  public async Task Refund_plus_fee_always_equals_the_reserved_amount()
  {
    var b = CompletedBooking();
    var (service, wallet, txn) = Make(b, percentage: 33m, flat: 0.5m);

    var result = await service.Terminate("user-1", b.Principal.Id, BeforeDeparture);

    result.IsSuccess().Should().BeTrue();
    // fee = round2(0.5 + 16 * 0.33) = 5.78, refund = 10.22
    wallet.LastDepositAmount.Should().Be(10.22m);
    var record = txn.Records.Single(r => r.Type == TransactionType.BookingTerminated);
    (record.Amount + 5.78m).Should().Be(Cost, "refund + fee = amount, to the cent");
  }

  // ---- fakes ----

  private sealed class FixedTerminationFeeRepository(
    decimal? percentage,
    decimal? flat,
    decimal? cap
  ) : IFeeRepository
  {
    public Task<Result<FeeChange?>> GetCurrent(FeeType type) =>
      Task.FromResult<Result<FeeChange?>>(
        type == FeeType.Termination && (percentage != null || flat != null || cap != null)
          ? new FeeChange
          {
            Id = Guid.NewGuid(),
            Type = type,
            Percentage = percentage ?? 0m,
            FlatAmount = flat ?? 0m,
            Cap = cap,
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
      decimal? cap2,
      DateTime? effectiveAt
    ) => throw new NotSupportedException();

    public Task<Result<FeeChange?>> CancelUpcoming(Guid id) => throw new NotSupportedException();
  }

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FakeCdc : IBookingCdcRepository
  {
    public Task<Result<Unit>> Add(string action) => Task.FromResult((Result<Unit>)new Unit());
  }

  private sealed class FakeTerminator : IBookingTerminatorRepository
  {
    public Task<Result<Unit>> Terminate(BookingTermination termination) =>
      Task.FromResult((Result<Unit>)new Unit());
  }

  private sealed class FakeNotifier : IBookingNotificationService
  {
    public Task<Result<Unit>> NotifyBookingCompleted(Booking booking) =>
      Task.FromResult((Result<Unit>)new Unit());

    public Task<Result<Unit>> NotifyBookingCancelled(Booking booking) =>
      Task.FromResult((Result<Unit>)new Unit());

    public Task<Result<Unit>> NotifyBookingTerminated(Booking booking) =>
      Task.FromResult((Result<Unit>)new Unit());

    public Task<Result<Unit>> NotifyBookingRefunded(Booking booking) =>
      Task.FromResult((Result<Unit>)new Unit());

    public Task<Result<Unit>> NotifyBookingDuplicate(Booking booking) =>
      Task.FromResult((Result<Unit>)new Unit());

    public Task<Result<Unit>> NotifyBookingManualIntervention(Booking booking) =>
      Task.FromResult((Result<Unit>)new Unit());
  }

  private sealed class FakeBookingRepository(Booking booking) : IBookingRepository
  {
    private BookStatus? statusOverride;

    private Booking Current()
    {
      var p = booking.Principal with
      {
        Status =
          statusOverride == null
            ? booking.Principal.Status
            : booking.Principal.Status with
            {
              Status = statusOverride.Value,
            },
      };
      return booking with { Principal = p };
    }

    public Task<Result<Booking?>> Get(string? userId, Guid id) =>
      Task.FromResult((Result<Booking?>)Current());

    public Task<Result<BookingPrincipal?>> Update(
      string? userId,
      Guid id,
      BookingStatus? status,
      BookingRecord? record,
      BookingComplete? complete
    )
    {
      if (status != null)
        statusOverride = status.Status;
      return Task.FromResult((Result<BookingPrincipal?>)Current().Principal);
    }

    public Task<Result<BookingPrincipal?>> Prioritize(string? userId, Guid id, decimal fee) =>
      throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> IncrementRecoveryRetries(Guid id) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search) =>
      throw new NotImplementedException();

    public Task<Result<int>> SearchCount(BookingSearch search) =>
      throw new NotImplementedException();

    public Task<Result<BookingQueuePosition?>> QueuePosition(string? userId, Guid id) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingStatRow>>> Stats(BookingStatsQuery query) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingPrincipal>>> RefundList(DateOnly date, TimeOnly time) =>
      throw new NotImplementedException();

    public Task<Result<BookingPrincipal>> Create(
      string userId,
      Guid transactionId,
      BookingRecord record
    ) => throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Reserve(
      TrainDirection direction,
      DateOnly date,
      TimeOnly time
    ) => throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(string? userId, Guid id) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingCount>>> Count(
      DateOnly date,
      TimeOnly time,
      DateOnly? filterDate,
      TrainDirection? filterDirection
    ) => throw new NotImplementedException();
  }

  private sealed class FakeWalletRepository : IWalletRepository
  {
    public int DepositCalls { get; private set; }
    public decimal? LastDepositAmount { get; private set; }

    private static WalletPrincipal Wallet(Guid id) =>
      new()
      {
        Id = id,
        UserId = "user-1",
        Record = new WalletRecord
        {
          Usable = 100m,
          WithdrawReserve = 0m,
          BookingReserve = 0m,
        },
      };

    public Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount)
    {
      DepositCalls++;
      LastDepositAmount = amount;
      return Task.FromResult((Result<WalletPrincipal?>)Wallet(id));
    }

    public Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> BookEnd(Guid id, decimal revert, decimal collect) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Wallet?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<Wallet?>> GetByUserId(string userId) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> PrepareWithdraw(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Withdraw(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> CancelWithdraw(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> BookStart(Guid id, decimal amount) =>
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
      return Task.FromResult(
        (Result<TransactionPrincipal>)
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
}
