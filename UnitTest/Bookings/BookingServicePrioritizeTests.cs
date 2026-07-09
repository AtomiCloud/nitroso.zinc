using CSharp_Result;
using Domain;
using Domain.Booking;
using Domain.Exceptions;
using Domain.Passenger;
using Domain.Timings;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Bookings;

// Prioritize guards + money movement, and the priority-fee refund on the
// Refund/Cancel flows. Invariants: the fee is collected at most once and only
// from an eligible owner's Pending, not-yet-priority booking; the ledger row
// always matches the wallet movement; Refunded/Cancelled return the snapshot
// fee, Terminated/Completed keep it.
public class BookingServicePrioritizeTests
{
  private const decimal Fee = 10m;
  private const decimal Cost = 16m;

  private static (
    BookingService Service,
    FakeBookingRepository Repo,
    FakeWalletRepository Wallet,
    FakeTransactionRepository Txn
  ) Make(
    Booking? booking,
    PrioritySettingsRecord? settings = null,
    bool allowlisted = false,
    bool walletInsufficient = false
  )
  {
    var repo = new FakeBookingRepository(booking);
    var wallet = new FakeWalletRepository(walletInsufficient);
    var txn = new FakeTransactionRepository();
    var service = new BookingService(
      repo,
      null!,
      wallet,
      txn,
      new PassThroughTransactionManager(),
      new TransactionGenerator(new FixedRefundCalculator()),
      new FixedRefundCalculator(),
      new FakeTerminator(),
      new FakeCdc(),
      new FakeNotifier(),
      new FakePrioritySettingsRepository(settings),
      new FakePriorityAccessRepository(allowlisted),
      NullLogger<BookingService>.Instance
    );
    return (service, repo, wallet, txn);
  }

  private static Booking BookingWith(
    BookStatus status,
    bool priority = false,
    decimal? priorityFee = null
  )
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
        Status = new BookingStatus { Status = status, CompletedAt = null },
        Complete = new BookingComplete
        {
          Ticket = null,
          BookingNumber = null,
          TicketNumber = null,
        },
        Priority = priority,
        PriorityFee = priorityFee,
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

  // ---- Prioritize ----

  [Fact]
  public async Task Prioritize_pending_eligible_collects_fee_and_books_the_ledger()
  {
    var b = BookingWith(BookStatus.Pending);
    var (service, repo, wallet, txn) = Make(b, allowlisted: true);

    var result = await service.Prioritize("user-1", b.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    wallet.CollectCalls.Should().Be(1);
    wallet.LastCollectAmount.Should().Be(Fee);
    txn.Records.Should().ContainSingle();
    txn.Records[0].Type.Should().Be(TransactionType.PriorityFee);
    txn.Records[0].Amount.Should().Be(Fee);
    txn.Records[0].From.Should().Be(Accounts.Usable.DisplayName);
    txn.Records[0].To.Should().Be(Accounts.PriorityFee.DisplayName);
    repo.PrioritizeCalls.Should().Be(1);
    repo.LastPrioritizeFee.Should().Be(Fee, "the charged fee is snapshotted for the refund path");
    result.SuccessOrDefault()!.Priority.Should().BeTrue();
  }

  [Theory]
  [InlineData(BookStatus.Buying)]
  [InlineData(BookStatus.Completed)]
  [InlineData(BookStatus.Cancelled)]
  [InlineData(BookStatus.Refunded)]
  [InlineData(BookStatus.Terminated)]
  [InlineData(BookStatus.Recovering)]
  [InlineData(BookStatus.Duplicate)]
  [InlineData(BookStatus.RequireManualIntervention)]
  public async Task Prioritize_non_pending_is_rejected_and_moves_no_money(BookStatus status)
  {
    var b = BookingWith(status);
    var (service, repo, wallet, txn) = Make(b, allowlisted: true);

    var result = await service.Prioritize("user-1", b.Principal.Id);

    result.IsSuccess().Should().BeFalse($"a '{status}' booking must not be prioritized");
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    wallet.CollectCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
    repo.PrioritizeCalls.Should().Be(0);
  }

  [Fact]
  public async Task Prioritize_already_priority_is_rejected_and_never_double_charges()
  {
    var b = BookingWith(BookStatus.Pending, priority: true, priorityFee: Fee);
    var (service, repo, wallet, txn) = Make(b, allowlisted: true);

    var result = await service.Prioritize("user-1", b.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    wallet.CollectCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
    repo.PrioritizeCalls.Should().Be(0);
  }

  [Fact]
  public async Task Prioritize_ineligible_owner_is_rejected_and_moves_no_money()
  {
    var b = BookingWith(BookStatus.Pending);
    var (service, repo, wallet, txn) = Make(b, allowlisted: false);

    var result = await service.Prioritize("user-1", b.Principal.Id);

    result.IsSuccess().Should().BeFalse("neither allowlisted nor allow-all");
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    wallet.CollectCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
    repo.PrioritizeCalls.Should().Be(0);
  }

  [Fact]
  public async Task Prioritize_allowed_via_allow_all()
  {
    var b = BookingWith(BookStatus.Pending);
    var settings = PrioritySettingsRecord.Default with { AllowAll = true };
    var (service, repo, wallet, _) = Make(b, settings, allowlisted: false);

    var result = await service.Prioritize("user-1", b.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    wallet.CollectCalls.Should().Be(1);
    repo.PrioritizeCalls.Should().Be(1);
  }

  [Fact]
  public async Task Prioritize_outside_the_window_is_rejected()
  {
    // pin a 1-hour window that deterministically excludes "now" regardless of
    // when the test runs: [now+1h, now+2h) in SGT (wrap-around is handled by
    // the rules, so crossing midnight is fine)
    var nowSgt = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
    var b = BookingWith(BookStatus.Pending);
    var settings = PrioritySettingsRecord.Default with
    {
      AllowAll = true,
      WindowStartSgt = nowSgt.AddHours(1),
      WindowEndSgt = nowSgt.AddHours(2),
    };
    var (service, _, wallet, txn) = Make(b, settings);

    var result = await service.Prioritize("user-1", b.Principal.Id);

    result.IsSuccess().Should().BeFalse("now is outside the configured window");
    wallet.CollectCalls.Should().Be(0);
    txn.Records.Should().BeEmpty();
  }

  [Fact]
  public void Equal_window_bounds_mean_all_day_not_never()
  {
    // an admin writing 00:00 -> 00:00 means "all day"; the strict half-open
    // reading would silently brick prioritization
    PriorityRules
      .WindowOpen(new TimeOnly(12, 0), new TimeOnly(12, 0), new TimeOnly(3, 33))
      .Should()
      .BeTrue();
    PriorityRules
      .WindowOpen(new TimeOnly(0, 0), new TimeOnly(0, 0), new TimeOnly(12, 0))
      .Should()
      .BeTrue();
  }

  [Fact]
  public async Task Prioritize_with_insufficient_balance_fails_and_never_flags_the_booking()
  {
    var b = BookingWith(BookStatus.Pending);
    var (service, repo, wallet, txn) = Make(b, allowlisted: true, walletInsufficient: true);

    var result = await service.Prioritize("user-1", b.Principal.Id);

    result.IsSuccess().Should().BeFalse("the collect failed");
    wallet.CollectCalls.Should().Be(1);
    txn.Records.Should().BeEmpty("no ledger row without a successful collect");
    repo.PrioritizeCalls.Should().Be(0, "the booking must not be flagged priority unpaid");
  }

  [Fact]
  public async Task Prioritize_with_zero_fee_flags_without_wallet_or_ledger()
  {
    var b = BookingWith(BookStatus.Pending);
    var settings = PrioritySettingsRecord.Default with { Fee = 0m, AllowAll = true };
    var (service, repo, wallet, txn) = Make(b, settings);

    var result = await service.Prioritize("user-1", b.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    wallet.CollectCalls.Should().Be(0, "a zero fee moves no money");
    txn.Records.Should().BeEmpty("a 'SGD 0.00 charged' ledger row would be noise");
    repo.PrioritizeCalls.Should().Be(1);
    repo.LastPrioritizeFee.Should().Be(0m);
  }

  [Fact]
  public async Task PriorityEligibility_reports_fee_and_defaults()
  {
    var (service, _, _, _) = Make(null, allowlisted: true);

    var result = await service.PriorityEligibility("user-1");

    result.IsSuccess().Should().BeTrue();
    result.SuccessOrDefault().Eligible.Should().BeTrue();
    result.SuccessOrDefault().Fee.Should().Be(Fee, "the default fee is 10");
  }

  // ---- priority fee refunds ----

  [Fact]
  public async Task Refund_of_priority_booking_returns_fee_with_its_own_ledger_row()
  {
    var b = BookingWith(BookStatus.Pending, priority: true, priorityFee: Fee);
    var (service, _, wallet, txn) = Make(b);

    var result = await service.Refund(b.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    wallet.BookEndCalls.Should().Be(1, "the booking amount refund is untouched");
    wallet.LastBookEndRevert.Should().Be(Cost);
    wallet.DepositCalls.Should().Be(1, "the priority fee goes back to Usable");
    wallet.LastDepositAmount.Should().Be(Fee);
    txn.Records.Should().HaveCount(2);
    txn.Records[0].Type.Should().Be(TransactionType.BookingRefund);
    txn.Records[1].Type.Should().Be(TransactionType.PriorityFee);
    txn.Records[1].Amount.Should().Be(Fee);
    txn.Records[1].From.Should().Be(Accounts.PriorityFee.DisplayName);
    txn.Records[1].To.Should().Be(Accounts.Usable.DisplayName);
  }

  [Fact]
  public async Task Cancel_of_priority_booking_returns_fee_with_its_own_ledger_row()
  {
    var b = BookingWith(BookStatus.Pending, priority: true, priorityFee: Fee);
    var (service, _, wallet, txn) = Make(b);

    var result = await service.Cancel("user-1", b.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    wallet.DepositCalls.Should().Be(1);
    wallet.LastDepositAmount.Should().Be(Fee);
    txn.Records.Should().HaveCount(2);
    txn.Records[0].Type.Should().Be(TransactionType.BookingCancel);
    txn.Records[1].Type.Should().Be(TransactionType.PriorityFee);
    txn.Records[1].To.Should().Be(Accounts.Usable.DisplayName);
  }

  [Fact]
  public async Task Refund_of_non_priority_booking_refunds_no_fee()
  {
    var b = BookingWith(BookStatus.Pending);
    var (service, _, wallet, txn) = Make(b);

    var result = await service.Refund(b.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    wallet.DepositCalls.Should().Be(0);
    txn.Records.Should().ContainSingle(r => r.Type == TransactionType.BookingRefund);
    txn.Records.Should().NotContain(r => r.Type == TransactionType.PriorityFee);
  }

  [Fact]
  public async Task Terminate_keeps_the_priority_fee()
  {
    var b = BookingWith(BookStatus.Completed, priority: true, priorityFee: Fee);
    var (service, _, wallet, txn) = Make(b);

    var result = await service.Terminate(
      "user-1",
      b.Principal.Id,
      DateTime.UtcNow // well before the 2026 departure
    );

    result.IsSuccess().Should().BeTrue();
    txn.Records.Should().NotContain(
      r => r.Type == TransactionType.PriorityFee,
      "the queue jump was consumed — a secured ticket keeps the fee"
    );
    wallet.LastDepositAmount.Should().Be(
      Cost * 0.5m,
      "only the termination refund is deposited, never the priority fee"
    );
  }

  // ---- fakes ----

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FixedRefundCalculator : IRefundCalculator
  {
    public decimal RefundRate => 0.5m;
    public decimal PenaltyRate => 0.5m;
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

  private sealed class FakePrioritySettingsRepository(PrioritySettingsRecord? settings)
    : IPrioritySettingsRepository
  {
    public Task<Result<PrioritySettingsPrincipal?>> GetCurrent() =>
      Task.FromResult(
        (Result<PrioritySettingsPrincipal?>)(
          settings == null
            ? null
            : new PrioritySettingsPrincipal
            {
              Id = Guid.NewGuid(),
              CreatedAt = DateTime.UtcNow,
              Record = settings,
            }
        )
      );

    public Task<Result<PrioritySettingsPrincipal>> Create(PrioritySettingsRecord record) =>
      throw new NotImplementedException();
  }

  private sealed class FakePriorityAccessRepository(bool allowlisted)
    : IPriorityAccessRepository
  {
    public Task<Result<bool>> Contains(string userId) =>
      Task.FromResult((Result<bool>)allowlisted);

    public Task<Result<IEnumerable<PriorityAccess>>> List() =>
      throw new NotImplementedException();

    public Task<Result<PriorityAccess>> Add(string userId) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Remove(string userId) => throw new NotImplementedException();
  }

  private sealed class FakeBookingRepository(Booking? booking) : IBookingRepository
  {
    private BookStatus? statusOverride;
    private bool priorityOverride;
    private decimal? feeOverride;

    public int PrioritizeCalls { get; private set; }
    public decimal? LastPrioritizeFee { get; private set; }
    public List<BookingStatus> StatusWrites { get; } = [];

    private Booking? Current()
    {
      if (booking == null)
        return null;
      var p = booking.Principal with
      {
        Status =
          statusOverride == null
            ? booking.Principal.Status
            : booking.Principal.Status with
            {
              Status = statusOverride.Value,
            },
        Priority = priorityOverride || booking.Principal.Priority,
        PriorityFee = feeOverride ?? booking.Principal.PriorityFee,
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
      {
        StatusWrites.Add(status);
        statusOverride = status.Status;
      }
      return Task.FromResult((Result<BookingPrincipal?>)Current()!.Principal);
    }

    public Task<Result<BookingPrincipal?>> Prioritize(string? userId, Guid id, decimal fee)
    {
      PrioritizeCalls++;
      LastPrioritizeFee = fee;
      priorityOverride = true;
      feeOverride = fee;
      return Task.FromResult((Result<BookingPrincipal?>)Current()!.Principal);
    }

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

  private sealed class FakeWalletRepository(bool insufficient) : IWalletRepository
  {
    public int CollectCalls { get; private set; }
    public int DepositCalls { get; private set; }
    public int BookEndCalls { get; private set; }
    public decimal? LastCollectAmount { get; private set; }
    public decimal? LastDepositAmount { get; private set; }
    public decimal? LastBookEndRevert { get; private set; }

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

    public Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount)
    {
      CollectCalls++;
      LastCollectAmount = amount;
      if (insufficient)
        return Task.FromResult(
          (Result<WalletPrincipal?>)new InvalidOperationException("insufficient balance")
        );
      return Task.FromResult((Result<WalletPrincipal?>)Wallet(id));
    }

    public Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount)
    {
      DepositCalls++;
      LastDepositAmount = amount;
      return Task.FromResult((Result<WalletPrincipal?>)Wallet(id));
    }

    public Task<Result<WalletPrincipal?>> BookEnd(Guid id, decimal revert, decimal collect)
    {
      BookEndCalls++;
      LastBookEndRevert = revert;
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
      throw new NotImplementedException();

    public Task<Result<Transaction?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }
}
