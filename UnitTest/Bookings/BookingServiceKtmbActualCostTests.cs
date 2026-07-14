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

// The actual-KTMB-cost capture paths: Complete() persists the optional
// amount/currency tin sends alongside the ticket; RecordKtmbActualCost() is
// the after-the-fact backfill — Completed-only, and IDEMPOTENT: once a cost
// is recorded the stored values are returned as-is and never overwritten (a
// retried backfill must not rewrite history). AttachTicket() repairs ticket
// artefacts without disturbing a recorded cost.
public class BookingServiceKtmbActualCostTests
{
  private const decimal Cost = 26m;

  private static (BookingService Service, FakeBookingRepository Repo) Make(Booking booking)
  {
    var repo = new FakeBookingRepository(booking);
    var service = new BookingService(
      repo,
      new FakeStorage(),
      new FakeWalletRepository(),
      new FakeTransactionRepository(),
      new PassThroughTransactionManager(),
      new TransactionGenerator(),
      null!,
      null!,
      new FakeCdc(),
      new FakeNotifier(),
      null!,
      null!,
      null!,
      NullLogger<BookingService>.Instance
    );
    return (service, repo);
  }

  private static Booking BookingWith(BookStatus status, BookingComplete complete)
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
        Status = new BookingStatus
        {
          Status = status,
          CompletedAt = status == BookStatus.Completed ? DateTime.UtcNow : null,
        },
        Complete = complete,
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

  private static readonly BookingComplete Uncaptured = new()
  {
    Ticket = null,
    BookingNumber = null,
    TicketNumber = null,
  };

  private static readonly BookingComplete CompletedWithoutCost = new()
  {
    Ticket = "ticket-file",
    BookingNumber = "BN-1",
    TicketNumber = "TN-1",
  };

  private static readonly BookingComplete CompletedWithCost = new()
  {
    Ticket = "ticket-file",
    BookingNumber = "BN-1",
    TicketNumber = "TN-1",
    KtmbAmount = 35.5m,
    KtmbCurrency = "MYR",
  };

  private static readonly BookingKtmbCost NewCost = new() { Amount = 40m, Currency = "SGD" };

  // ---- Complete() capture ----

  [Fact]
  public async Task Complete_with_ktmb_cost_stores_amount_and_currency_on_the_completion()
  {
    var booking = BookingWith(BookStatus.Buying, Uncaptured);
    var (service, repo) = Make(booking);

    var result = await service.Complete(
      booking.Principal.Id,
      "BN-1",
      "TN-1",
      new MemoryStream([1, 2, 3]),
      new BookingKtmbCost { Amount = 35.5m, Currency = "MYR" }
    );

    result.IsSuccess().Should().BeTrue();
    repo.LastCompleteWritten!.KtmbAmount.Should().Be(35.5m);
    repo.LastCompleteWritten!.KtmbCurrency.Should().Be("MYR");
    repo.LastCompleteWritten!.BookingNumber.Should().Be("BN-1");
    repo.LastCompleteWritten!.TicketNumber.Should().Be("TN-1");
  }

  [Fact]
  public async Task Complete_without_ktmb_cost_stores_null_for_the_old_client_path()
  {
    var booking = BookingWith(BookStatus.Buying, Uncaptured);
    var (service, repo) = Make(booking);

    var result = await service.Complete(
      booking.Principal.Id,
      "BN-1",
      "TN-1",
      new MemoryStream([1, 2, 3])
    );

    result.IsSuccess().Should().BeTrue();
    repo.LastCompleteWritten!.KtmbAmount.Should().BeNull();
    repo.LastCompleteWritten!.KtmbCurrency.Should().BeNull();
  }

  // ---- RecordKtmbActualCost() backfill ----

  [Fact]
  public async Task Backfill_on_a_completed_booking_without_cost_records_it()
  {
    var booking = BookingWith(BookStatus.Completed, CompletedWithoutCost);
    var (service, repo) = Make(booking);

    var result = await service.RecordKtmbActualCost(booking.Principal.Id, NewCost);

    result.IsSuccess().Should().BeTrue();
    result.SuccessOrDefault()!.Amount.Should().Be(40m);
    result.SuccessOrDefault()!.Currency.Should().Be("SGD");
    repo.UpdateCalls.Should().Be(1);
    repo.LastCompleteWritten!.KtmbAmount.Should().Be(40m);
    repo.LastCompleteWritten!.KtmbCurrency.Should().Be("SGD");
    // the ticket artefacts must survive the backfill untouched
    repo.LastCompleteWritten!.Ticket.Should().Be("ticket-file");
    repo.LastCompleteWritten!.BookingNumber.Should().Be("BN-1");
    repo.LastCompleteWritten!.TicketNumber.Should().Be("TN-1");
  }

  [Fact]
  public async Task Backfill_is_idempotent_and_never_overwrites_a_recorded_cost()
  {
    var booking = BookingWith(BookStatus.Completed, CompletedWithCost);
    var (service, repo) = Make(booking);

    var result = await service.RecordKtmbActualCost(booking.Principal.Id, NewCost);

    // success reporting the STORED values, not the caller's
    result.IsSuccess().Should().BeTrue("a retried backfill must succeed without rewriting history");
    result.SuccessOrDefault()!.Amount.Should().Be(35.5m);
    result.SuccessOrDefault()!.Currency.Should().Be("MYR");
    repo.UpdateCalls.Should().Be(0, "an already-recorded cost must never be overwritten");
  }

  [Theory]
  [InlineData(BookStatus.Pending)]
  [InlineData(BookStatus.Buying)]
  [InlineData(BookStatus.Recovering)]
  [InlineData(BookStatus.Cancelled)]
  [InlineData(BookStatus.Refunded)]
  [InlineData(BookStatus.Terminated)]
  [InlineData(BookStatus.Duplicate)]
  [InlineData(BookStatus.RequireManualIntervention)]
  public async Task Backfill_on_a_non_completed_booking_is_rejected(BookStatus status)
  {
    var booking = BookingWith(status, Uncaptured);
    var (service, repo) = Make(booking);

    var result = await service.RecordKtmbActualCost(booking.Principal.Id, NewCost);

    result.IsSuccess().Should().BeFalse($"a '{status}' booking has no actual KTMB purchase to cost");
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0);
  }

  [Fact]
  public async Task Backfill_on_a_missing_booking_returns_null()
  {
    var (service, _) = Make(null!);

    var result = await service.RecordKtmbActualCost(Guid.NewGuid(), NewCost);

    result.IsSuccess().Should().BeTrue();
    result.SuccessOrDefault().Should().BeNull();
  }

  // ---- AttachTicket() preservation ----

  [Fact]
  public async Task AttachTicket_preserves_a_recorded_ktmb_cost()
  {
    var booking = BookingWith(BookStatus.Completed, CompletedWithCost);
    var (service, repo) = Make(booking);

    var result = await service.AttachTicket(
      booking.Principal.Id,
      null,
      null,
      new MemoryStream([1, 2, 3])
    );

    result.IsSuccess().Should().BeTrue();
    repo.LastCompleteWritten!.KtmbAmount.Should().Be(35.5m);
    repo.LastCompleteWritten!.KtmbCurrency.Should().Be("MYR");
  }

  // ---- fakes ----

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FakeStorage : IBookingStorage
  {
    public Task<Result<string>> Save(Stream stream) => Task.FromResult((Result<string>)"file-id");

    public Task<Result<string>> Get(string key) => Task.FromResult((Result<string>)"file-url");

    public Task<Result<bool>> Exists(string key) => Task.FromResult((Result<bool>)true);
  }

  private sealed class FakeCdc : IBookingCdcRepository
  {
    public Task<Result<Unit>> Add(string action) => Task.FromResult((Result<Unit>)new Unit());
  }

  private sealed class FakeNotifier : IBookingNotificationService
  {
    public Task<Result<Unit>> NotifyBookingCompleted(Booking booking) =>
      Task.FromResult((Result<Unit>)new Unit());

    public Task<Result<Unit>> NotifyBookingCancelled(Booking booking) =>
      throw new NotImplementedException();

    public Task<Result<Unit>> NotifyBookingTerminated(Booking booking) =>
      throw new NotImplementedException();

    public Task<Result<Unit>> NotifyBookingRefunded(Booking booking) =>
      throw new NotImplementedException();

    public Task<Result<Unit>> NotifyBookingDuplicate(Booking booking) =>
      throw new NotImplementedException();

    public Task<Result<Unit>> NotifyBookingManualIntervention(Booking booking) =>
      throw new NotImplementedException();
  }

  private sealed class FakeBookingRepository(Booking? booking) : IBookingRepository
  {
    private BookingComplete? completeOverride;

    private BookingStatus? statusOverride;

    public int UpdateCalls { get; private set; }

    public BookingComplete? LastCompleteWritten { get; private set; }

    private Booking? Current()
    {
      if (booking == null)
        return null;
      var p = booking.Principal with
      {
        Complete = completeOverride ?? booking.Principal.Complete,
        Status = statusOverride ?? booking.Principal.Status,
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
      UpdateCalls++;
      if (complete != null)
      {
        LastCompleteWritten = complete;
        completeOverride = complete;
      }
      if (status != null)
        statusOverride = status;
      return Task.FromResult((Result<BookingPrincipal?>)Current()!.Principal);
    }

    public Task<Result<IEnumerable<BookingKtmbCostMissing>>> ListMissingKtmbCost(
      int limit,
      int skip
    ) => throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Prioritize(
      string? userId,
      Guid id,
      decimal? fee,
      string? grantedBy
    ) => throw new NotImplementedException();

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
      BookingRecord record,
      BookingPriceBreakdown? breakdown
    ) => throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Reserve(
      TrainDirection direction,
      DateOnly date,
      TimeOnly time
    ) => throw new NotImplementedException();

    public Task<Result<int>> CountSlotPriority(
      TrainDirection direction,
      DateOnly date,
      TimeOnly time
    ) => Task.FromResult((Result<int>)0);

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

    public Task<Result<WalletPrincipal?>> BookEnd(Guid id, decimal revert, decimal collect) =>
      Task.FromResult((Result<WalletPrincipal?>)Wallet(id));

    public Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount) =>
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
    public Task<Result<TransactionPrincipal>> Create(
      Guid walletId,
      TransactionRecord record,
      Guid? paymentId = null
    ) =>
      Task.FromResult(
        (Result<TransactionPrincipal>)
          new TransactionPrincipal
          {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Record = record,
          }
      );

    public Task<Result<IEnumerable<TransactionPrincipal>>> Search(TransactionSearch search) =>
      throw new NotSupportedException();

    public Task<Result<Domain.Transaction.Transaction?>> Get(Guid id, string? userId) =>
      throw new NotSupportedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotSupportedException();
  }
}
