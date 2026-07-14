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

namespace UnitTest.Bookings;

// CompleteNoCollect is the admin bypass that finalises a RequireManualIntervention
// booking WITHOUT moving money — it must never call the wallet or write a ledger
// row. These tests pin that invariant by passing NULL walletRepo/transactionRepo:
// if CompleteNoCollect ever touched them the test would NRE. They also prove the
// guard (RequireManualIntervention only) and that the ticket + Completed status
// are written.
public class BookingServiceCompleteNoCollectTests
{
  // walletRepo (arg 2) and transactionRepo (arg 3) are deliberately null — the
  // reserve-untouched invariant means CompleteNoCollect must never invoke them.
  private static BookingService MakeService(FakeBookingRepository repo) =>
    new(
      repo,
      new FakeStorage(),
      null!,
      null!,
      new PassThroughTransactionManager(),
      null!,
      null!,
      null!,
      new FakeCdc(),
      new FakeNotifier(),
      null!,
      null!,
      null!,
      null!
    );

  private static Booking BookingWith(BookStatus status, string? bookingNumber)
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
          Date = new DateOnly(2026, 7, 3),
          Time = new TimeOnly(8, 0),
          Direction = TrainDirection.WToJ,
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
          Ticket = bookingNumber == null ? null : "ticket-file",
          BookingNumber = bookingNumber,
          TicketNumber = bookingNumber == null ? null : "TN-old",
        },
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
          Name = "BookingRequest",
          Description = "reserve",
          Type = TransactionType.BookingRequest,
          Amount = 26.0m,
          From = "Usable",
          To = "BookingReserve",
        },
      },
      Wallet = new WalletPrincipal
      {
        Id = Guid.NewGuid(),
        UserId = "user-1",
        Record = new WalletRecord { Usable = 100m, WithdrawReserve = 0m, BookingReserve = 26m },
      },
    };
  }

  [Fact]
  public async Task CompleteNoCollect_from_manual_intervention_completes_saves_ticket_and_moves_no_money()
  {
    var booking = BookingWith(BookStatus.RequireManualIntervention, bookingNumber: null);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo)
      .CompleteNoCollect(booking.Principal.Id, "BN-1", "TN-1", new MemoryStream([1, 2, 3]));

    // reaching success at all proves no money path ran (null wallet/transaction repos)
    result.IsSuccess().Should().BeTrue("an admin may finalise a RequireManualIntervention booking without collecting");
    repo.UpdateCalls.Should().Be(1);
    repo.LastStatusWritten!.Status.Should().Be(BookStatus.Completed);
    repo.LastCompleteWritten!.BookingNumber.Should().Be("BN-1");
    repo.LastCompleteWritten!.TicketNumber.Should().Be("TN-1");
  }

  [Theory]
  [InlineData(BookStatus.Pending)]
  [InlineData(BookStatus.Buying)]
  [InlineData(BookStatus.Recovering)]
  [InlineData(BookStatus.Completed)]
  [InlineData(BookStatus.Cancelled)]
  [InlineData(BookStatus.Refunded)]
  [InlineData(BookStatus.Terminated)]
  [InlineData(BookStatus.Duplicate)]
  public async Task CompleteNoCollect_from_non_manual_intervention_is_rejected(BookStatus status)
  {
    var booking = BookingWith(status, bookingNumber: null);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo)
      .CompleteNoCollect(booking.Principal.Id, "BN-1", "TN-1", new MemoryStream([1]));

    result.IsSuccess().Should().BeFalse($"a '{status}' booking is not in the manual-intervention parking state");
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0, "no status write may happen when the guard fails");
  }

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
    public Task<Result<Unit>> NotifyBookingCompleted(Booking booking) => Task.FromResult((Result<Unit>)new Unit());
    public Task<Result<Unit>> NotifyBookingCancelled(Booking booking) => throw new NotImplementedException();
    public Task<Result<Unit>> NotifyBookingTerminated(Booking booking) => throw new NotImplementedException();
    public Task<Result<Unit>> NotifyBookingRefunded(Booking booking) => throw new NotImplementedException();
    public Task<Result<Unit>> NotifyBookingDuplicate(Booking booking) => throw new NotImplementedException();
    public Task<Result<Unit>> NotifyBookingManualIntervention(Booking booking) => throw new NotImplementedException();
  }

  private sealed class FakeBookingRepository(Booking? booking) : IBookingRepository
  {
    public int UpdateCalls { get; private set; }
    public BookingStatus? LastStatusWritten { get; private set; }
    public BookingComplete? LastCompleteWritten { get; private set; }

    public Task<Result<Booking?>> Get(string? userId, Guid id) =>
      Task.FromResult((Result<Booking?>)booking);

    public Task<Result<BookingPrincipal?>> Update(
      string? userId,
      Guid id,
      BookingStatus? status,
      BookingRecord? record,
      BookingComplete? complete
    )
    {
      UpdateCalls++;
      LastStatusWritten = status;
      LastCompleteWritten = complete;
      var principal = booking!.Principal with { Status = status! };
      return Task.FromResult((Result<BookingPrincipal?>)principal);
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

    public Task<Result<BookingPrincipal>> Create(string userId, Guid transactionId, BookingRecord record, BookingPriceBreakdown? breakdown = null) =>
      throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly time) =>
      throw new NotImplementedException();

    public Task<Result<int>> CountSlotPriority(TrainDirection direction, DateOnly date, TimeOnly time) =>
      Task.FromResult((Result<int>)0);

    public Task<Result<BookingPrincipal?>> Prioritize(string? userId, Guid id, decimal? fee, string? grantedBy = null) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingKtmbCostMissing>>> ListMissingKtmbCost(int limit, int skip) =>
      throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> IncrementRecoveryRetries(Guid id) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(string? userId, Guid id) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingCount>>> Count(
      DateOnly date,
      TimeOnly time,
      DateOnly? filterDate,
      TrainDirection? filterDirection
    ) => throw new NotImplementedException();
  }
}
