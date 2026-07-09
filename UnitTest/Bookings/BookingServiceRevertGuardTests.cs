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

// Guards on BookingService.Revert(id). Revert recycles a stuck 'Buying' booking
// back to 'Pending' (e.g. after a transient KTMB pay failure where no ticket was
// bought). The deleted, unguarded reverter could reverse ANY status — including a
// 'Completed' booking whose reserve was already collected — into 'Pending',
// corrupting the ledger, and could re-expose a captured ticket into a double-buy.
// These tests prove the transition is now a guarded, once-only transaction that
// only fires for an uncaptured 'Buying' booking and writes nothing otherwise.
public class BookingServiceRevertGuardTests
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
      null!
    );

  private static Booking BookingWith(
    BookStatus status,
    string? bookingNumber,
    DateTime? lastBuyingAt = null
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
        Status = new BookingStatus
        {
          Status = status,
          CompletedAt = null,
          LastBuyingAt = lastBuyingAt,
        },
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
  public async Task Revert_from_uncaptured_buying_transitions_to_pending()
  {
    var booking = BookingWith(BookStatus.Buying, bookingNumber: null);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: true);

    result.IsSuccess().Should().BeTrue("an uncaptured 'Buying' booking may be reverted to 'Pending'");
    repo.UpdateCalls.Should().Be(1);
    repo.LastStatusWritten!.Status.Should().Be(BookStatus.Pending);
  }

  [Fact]
  public async Task Revert_from_uncaptured_manual_intervention_transitions_to_pending()
  {
    var booking = BookingWith(BookStatus.RequireManualIntervention, bookingNumber: null);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: true);

    result.IsSuccess().Should().BeTrue("an uncaptured 'RequireManualIntervention' booking may be reverted to 'Pending'");
    repo.UpdateCalls.Should().Be(1);
    repo.LastStatusWritten!.Status.Should().Be(BookStatus.Pending);
  }

  [Theory]
  [InlineData(BookStatus.Pending)]
  [InlineData(BookStatus.Completed)]
  [InlineData(BookStatus.Cancelled)]
  [InlineData(BookStatus.Refunded)]
  [InlineData(BookStatus.Terminated)]
  [InlineData(BookStatus.Recovering)]
  [InlineData(BookStatus.Duplicate)]
  public async Task Revert_from_disallowed_status_is_rejected_and_writes_nothing(BookStatus status)
  {
    // A Completed booking additionally carries a BookingNumber (reserve already
    // collected); the others carry none. Either way the guard must reject — only
    // an uncaptured 'Buying' or 'RequireManualIntervention' booking may be reverted.
    var bookingNumber = status == BookStatus.Completed ? "BN-123" : null;
    var booking = BookingWith(status, bookingNumber);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: true);

    result.IsSuccess().Should().BeFalse($"a '{status}' booking must not be reverted to 'Pending'");
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0, "no status write may happen when the guard fails");
  }

  [Fact]
  public async Task Revert_of_captured_manual_intervention_booking_is_rejected()
  {
    // A RequireManualIntervention booking that already captured a ticket
    // (BookingNumber set) must never be reverted — re-exposing it would risk a
    // double-buy, and its reserve may already be resolved.
    var booking = BookingWith(BookStatus.RequireManualIntervention, bookingNumber: "BN-777");
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: true);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0);
  }

  [Fact]
  public async Task Revert_of_captured_buying_booking_is_rejected()
  {
    // Defense-in-depth: a 'Buying' booking that already captured a ticket
    // (BookingNumber set) must never be reverted — re-exposing it to the demand
    // pool would cause a double-buy / duplicate charge.
    var booking = BookingWith(BookStatus.Buying, bookingNumber: "BN-999");
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: true);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0);
  }

  // Non-force reverts are what the automated reverter cron issues: they must
  // additionally prove the booking is genuinely stuck (in Buying for more than
  // the 5-minute threshold) so the cron can never race a live purchase.

  [Fact]
  public async Task NonForce_revert_of_stale_uncaptured_buying_transitions_to_pending()
  {
    var booking = BookingWith(
      BookStatus.Buying,
      bookingNumber: null,
      lastBuyingAt: DateTime.UtcNow.AddMinutes(-10)
    );
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: false);

    result.IsSuccess().Should().BeTrue("a booking stuck in 'Buying' for over 5 minutes may be reverted");
    repo.UpdateCalls.Should().Be(1);
    repo.LastStatusWritten!.Status.Should().Be(BookStatus.Pending);
  }

  [Fact]
  public async Task NonForce_revert_of_fresh_buying_is_rejected()
  {
    // 1 minute into Buying: the buyer may still be mid-purchase — exactly the
    // race that corrupted bookings under the old unguarded reverter
    var booking = BookingWith(
      BookStatus.Buying,
      bookingNumber: null,
      lastBuyingAt: DateTime.UtcNow.AddMinutes(-1)
    );
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: false);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0);
  }

  [Fact]
  public async Task NonForce_revert_without_buying_timestamp_is_rejected()
  {
    // Rows predating the LastBuyingAt column have no stamp; the cron must not
    // guess their age
    var booking = BookingWith(BookStatus.Buying, bookingNumber: null, lastBuyingAt: null);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: false);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0);
  }

  [Fact]
  public async Task NonForce_revert_of_manual_intervention_is_rejected()
  {
    // RequireManualIntervention is a human-judgement state; only a forced
    // (admin) revert may re-expose it
    var booking = BookingWith(
      BookStatus.RequireManualIntervention,
      bookingNumber: null,
      lastBuyingAt: DateTime.UtcNow.AddMinutes(-30)
    );
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: false);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0);
  }

  [Fact]
  public async Task NonForce_revert_of_stale_captured_buying_is_rejected()
  {
    // Stuck AND captured: the ticket exists on KITS, so re-buying would
    // double-charge — recovery must handle it instead
    var booking = BookingWith(
      BookStatus.Buying,
      bookingNumber: "BN-555",
      lastBuyingAt: DateTime.UtcNow.AddMinutes(-30)
    );
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo).Revert(booking.Principal.Id, force: false);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0);
  }

  [Fact]
  public async Task Revert_of_missing_booking_is_rejected()
  {
    var repo = new FakeBookingRepository(null);

    var result = await MakeService(repo).Revert(Guid.NewGuid(), force: true);

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
