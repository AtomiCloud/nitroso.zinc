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

// Guards on BookingService.Buying(id). The buyer only ever drives a 'Pending'
// booking into 'Buying'; a blind status write from a stale tin/admin caller
// could otherwise reverse a terminal/recovered/completed booking into 'Buying'
// (stranding a collected ticket). These tests prove the transition is now a
// guarded, once-only transaction like the other recovery transitions.
public class BookingServiceBuyingGuardTests
{
  private static BookingService MakeService(FakeBookingRepository repo) =>
    new(
      repo,
      null!,
      null!,
      null!,
      new PassThroughTransactionManager(),
      null!,
      null!,
      null!,
      null!,
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
          TicketNumber = bookingNumber == null ? null : "TN-1",
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
        Record = new WalletRecord
        {
          Usable = 100m,
          WithdrawReserve = 0m,
          BookingReserve = 26m,
        },
      },
    };
  }

  [Fact]
  public async Task Buying_from_pending_transitions_to_buying()
  {
    var booking = BookingWith(BookStatus.Pending, bookingNumber: null);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Buying(booking.Principal.Id);

    result.IsSuccess().Should().BeTrue("a 'Pending' booking may enter 'Buying'");
    repo.UpdateCalls.Should().Be(1);
    repo.LastStatusWritten!.Status.Should().Be(BookStatus.Buying);
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
  public async Task Buying_from_non_pending_status_is_rejected_and_writes_nothing(BookStatus status)
  {
    // A Completed booking additionally carries a BookingNumber (reserve already
    // collected); the others carry none. Either way the guard must reject.
    var bookingNumber = status == BookStatus.Completed ? "BN-123" : null;
    var booking = BookingWith(status, bookingNumber);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Buying(booking.Principal.Id);

    result.IsSuccess().Should().BeFalse($"a '{status}' booking must not be pushed back into 'Buying'");
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0, "no status write may happen when the guard fails");
  }

  [Fact]
  public async Task Buying_of_captured_pending_booking_is_rejected()
  {
    // Defense-in-depth: a booking that already captured a ticket (BookingNumber
    // set) must never be pushed back into 'Buying' even if its status read as
    // 'Pending', because that reserve has been collected.
    var booking = BookingWith(BookStatus.Pending, bookingNumber: "BN-999");
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Buying(booking.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0);
  }

  [Fact]
  public async Task Buying_of_missing_booking_is_rejected()
  {
    var repo = new FakeBookingRepository(null);

    var result = await MakeService(repo).Buying(Guid.NewGuid());

    result.IsSuccess().Should().BeFalse();
    repo.UpdateCalls.Should().Be(0);
  }

  // Runs the wrapped unit of work inline, exactly as the real RepeatableRead
  // transaction would, so the guard read + status write are exercised end to end.
  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FakeBookingRepository(Booking? booking) : IBookingRepository
  {
    public int UpdateCalls { get; private set; }
    public BookingStatus? LastStatusWritten { get; private set; }

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

    public Task<Result<BookingPrincipal>> Create(string userId, Guid transactionId, BookingRecord record) =>
      throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly time) =>
      throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Prioritize(string? userId, Guid id, decimal fee) =>
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
