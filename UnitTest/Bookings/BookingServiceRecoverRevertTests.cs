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

// Guards on BookingService.RecoverRevert(id, maxRetries). RecoverRevert recycles
// a 'Recovering' booking back to 'Pending' for another purchase attempt while
// counting the attempt in RecoveryRetries. It must only fire for an uncaptured
// 'Recovering' booking, must refuse (with a distinguishable
// RecoveryRetriesExhaustedException, writing NOTHING) once the counter reaches
// the cap, and must never move money — the null wallet/transaction repos in the
// service would NRE if it ever did.
public class BookingServiceRecoverRevertTests
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
      null!,
      null!
    );

  private static Booking BookingWith(BookStatus status, string? bookingNumber, int retries = 0)
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
        RecoveryRetries = retries,
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
  public async Task RecoverRevert_from_recovering_increments_counter_and_reverts_to_pending()
  {
    var booking = BookingWith(BookStatus.Recovering, bookingNumber: null, retries: 0);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).RecoverRevert(booking.Principal.Id, maxRetries: 10);

    result.IsSuccess().Should().BeTrue("an uncaptured 'Recovering' booking below the cap may recycle");
    repo.IncrementCalls.Should().Be(1, "the attempt must be counted");
    repo.Retries.Should().Be(1, "the counter increments 0 -> 1");
    repo.UpdateCalls.Should().Be(1);
    repo.LastStatusWritten!.Status.Should().Be(BookStatus.Pending);
    repo.LastStatusWritten!.CompletedAt.Should().BeNull();
  }

  [Theory]
  [InlineData(BookStatus.Pending)]
  [InlineData(BookStatus.Buying)]
  [InlineData(BookStatus.Completed)]
  [InlineData(BookStatus.Cancelled)]
  [InlineData(BookStatus.Refunded)]
  [InlineData(BookStatus.Terminated)]
  [InlineData(BookStatus.Duplicate)]
  [InlineData(BookStatus.RequireManualIntervention)]
  public async Task RecoverRevert_from_non_recovering_status_is_rejected_and_writes_nothing(
    BookStatus status
  )
  {
    var bookingNumber = status == BookStatus.Completed ? "BN-123" : null;
    var booking = BookingWith(status, bookingNumber);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).RecoverRevert(booking.Principal.Id, maxRetries: 10);

    result.IsSuccess().Should().BeFalse($"a '{status}' booking must not be recycled to 'Pending'");
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.IncrementCalls.Should().Be(0, "a refused recycle must not consume a retry");
    repo.UpdateCalls.Should().Be(0, "no status write may happen when the guard fails");
  }

  [Fact]
  public async Task RecoverRevert_of_captured_recovering_booking_is_rejected()
  {
    // A booking that already captured a KTMB ticket must never re-enter the
    // demand pool: re-buying it would double-purchase.
    var booking = BookingWith(BookStatus.Recovering, bookingNumber: "BN-999");
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).RecoverRevert(booking.Principal.Id, maxRetries: 10);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.IncrementCalls.Should().Be(0);
    repo.UpdateCalls.Should().Be(0);
  }

  [Fact]
  public async Task RecoverRevert_at_cap_is_refused_with_exhausted_error_and_writes_nothing()
  {
    var booking = BookingWith(BookStatus.Recovering, bookingNumber: null, retries: 10);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).RecoverRevert(booking.Principal.Id, maxRetries: 10);

    result.IsSuccess().Should().BeFalse("the cap is exhausted");
    var e = result.FailureOrDefault().Should().BeOfType<RecoveryRetriesExhaustedException>().Subject;
    e.BookingId.Should().Be(booking.Principal.Id.ToString());
    e.Retries.Should().Be(10);
    e.MaxRetries.Should().Be(10);
    repo.IncrementCalls.Should().Be(0, "an exhausted booking must not keep counting");
    repo.UpdateCalls.Should().Be(0, "an exhausted booking must stay parked in 'Recovering'");
  }

  [Fact]
  public async Task RecoverRevert_last_allowed_retry_succeeds_then_next_is_exhausted()
  {
    // retries = 9 with cap 10: the 10th recycle is allowed (9 -> 10); once the
    // booking lands back in 'Recovering' the 11th call must be refused as
    // exhausted, not silently recycled again.
    var booking = BookingWith(BookStatus.Recovering, bookingNumber: null, retries: 9);
    var repo = new FakeBookingRepository(booking);
    var service = MakeService(repo);

    var tenth = await service.RecoverRevert(booking.Principal.Id, maxRetries: 10);
    tenth.IsSuccess().Should().BeTrue("retry 9 -> 10 is the last allowed recycle");
    repo.Retries.Should().Be(10);
    repo.LastStatusWritten!.Status.Should().Be(BookStatus.Pending);

    // the recoverer parks the booking in 'Recovering' again after another
    // failed purchase attempt
    repo.StatusOverride = BookStatus.Recovering;

    var eleventh = await service.RecoverRevert(booking.Principal.Id, maxRetries: 10);
    eleventh.IsSuccess().Should().BeFalse("the counter reached the cap");
    eleventh.FailureOrDefault().Should().BeOfType<RecoveryRetriesExhaustedException>();
    repo.IncrementCalls.Should().Be(1, "only the allowed recycle was counted");
    repo.UpdateCalls.Should().Be(1, "the exhausted call wrote nothing");
  }

  [Fact]
  public async Task RecoverRevert_of_missing_booking_is_rejected()
  {
    var repo = new FakeBookingRepository(null);

    var result = await MakeService(repo).RecoverRevert(Guid.NewGuid(), maxRetries: 10);

    result.IsSuccess().Should().BeFalse();
    repo.IncrementCalls.Should().Be(0);
    repo.UpdateCalls.Should().Be(0);
  }

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  // Stateful fake: tracks the retry counter and lets a test park the booking
  // back into 'Recovering' between calls (StatusOverride) to simulate the
  // recoverer failing the recycled attempt again.
  private sealed class FakeBookingRepository(Booking? booking) : IBookingRepository
  {
    public int UpdateCalls { get; private set; }
    public int IncrementCalls { get; private set; }
    public int Retries { get; private set; } = booking?.Principal.RecoveryRetries ?? 0;
    public BookingStatus? LastStatusWritten { get; private set; }
    public BookStatus? StatusOverride { get; set; }

    private Booking? Current()
    {
      if (booking == null)
        return null;
      var status = StatusOverride ?? booking.Principal.Status.Status;
      return booking with
      {
        Principal = booking.Principal with
        {
          Status = booking.Principal.Status with { Status = status },
          RecoveryRetries = Retries,
        },
      };
    }

    public Task<Result<Booking?>> Get(string? userId, Guid id) =>
      Task.FromResult((Result<Booking?>)Current());

    public Task<Result<BookingPrincipal?>> IncrementRecoveryRetries(Guid id)
    {
      IncrementCalls++;
      Retries++;
      return Task.FromResult((Result<BookingPrincipal?>)Current()!.Principal);
    }

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
      StatusOverride = status!.Status;
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

    public Task<Result<BookingPrincipal>> Create(string userId, Guid transactionId, BookingRecord record, BookingPriceBreakdown? breakdown = null) =>
      throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly time) =>
      throw new NotImplementedException();

    public Task<Result<int>> CountSlotPriority(TrainDirection direction, DateOnly date, TimeOnly time) =>
      Task.FromResult((Result<int>)0);

    public Task<Result<BookingPrincipal?>> Prioritize(string? userId, Guid id, decimal? fee, string? grantedBy = null) =>
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
