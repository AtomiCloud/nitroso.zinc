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

// Guards on BookingService.AttachTicket(id, bookingNo, ticketNo, file). Attach
// is a repair for ALREADY-Completed bookings (e.g. dangling ticket refs): it
// must never move money (null wallet/transaction repos would NRE), never change
// status, allow overwriting the ticket file reference, backfill only missing
// KTMB identifiers, and refuse conflicting non-null identifiers.
public class BookingServiceAttachTicketTests
{
  // walletRepo/transactionRepo/transactionGenerator deliberately null: attach
  // must never touch money.
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
      null!,
      null!,
      null!,
      null!,
      null!
    );

  private static Booking BookingWith(
    BookStatus status,
    string? ticket,
    string? bookingNumber,
    string? ticketNumber
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
        Status = new BookingStatus { Status = status, CompletedAt = DateTime.UtcNow },
        Complete = new BookingComplete
        {
          Ticket = ticket,
          BookingNumber = bookingNumber,
          TicketNumber = ticketNumber,
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
  public async Task AttachTicket_overwrites_dangling_ticket_ref_without_status_write()
  {
    // Ticket ref overwrite IS the repair — the old key may dangle in storage
    var booking = BookingWith(BookStatus.Completed, "old-dangling-key", "BN-1", "TN-1");
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo)
      .AttachTicket(booking.Principal.Id, null, null, new MemoryStream([1, 2, 3]));

    result.IsSuccess().Should().BeTrue("replacing a Completed booking's ticket file is the repair");
    repo.UpdateCalls.Should().Be(1);
    repo.LastStatusWritten.Should().BeNull("attach must never write status");
    repo.LastCompleteWritten!.Ticket.Should().Be("file-id", "the new upload replaces the old ref");
    repo.LastCompleteWritten!.BookingNumber.Should().Be("BN-1", "existing identifiers are kept");
    repo.LastCompleteWritten!.TicketNumber.Should().Be("TN-1");
  }

  [Fact]
  public async Task AttachTicket_backfills_missing_identifiers()
  {
    var booking = BookingWith(BookStatus.Completed, ticket: null, bookingNumber: null, ticketNumber: null);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo)
      .AttachTicket(booking.Principal.Id, "BN-2", "TN-2", new MemoryStream([1]));

    result.IsSuccess().Should().BeTrue();
    repo.LastCompleteWritten!.Ticket.Should().Be("file-id");
    repo.LastCompleteWritten!.BookingNumber.Should().Be("BN-2", "a null identifier may be backfilled");
    repo.LastCompleteWritten!.TicketNumber.Should().Be("TN-2");
  }

  [Fact]
  public async Task AttachTicket_keeps_existing_identifiers_when_same_values_are_provided()
  {
    var booking = BookingWith(BookStatus.Completed, "key", "BN-1", "TN-1");
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo)
      .AttachTicket(booking.Principal.Id, "BN-1", "TN-1", new MemoryStream([1]));

    result.IsSuccess().Should().BeTrue("re-supplying the identical identifiers is not a conflict");
    repo.LastCompleteWritten!.BookingNumber.Should().Be("BN-1");
    repo.LastCompleteWritten!.TicketNumber.Should().Be("TN-1");
  }

  [Theory]
  [InlineData("BN-DIFFERENT", "TN-1")]
  [InlineData("BN-1", "TN-DIFFERENT")]
  [InlineData("BN-DIFFERENT", "TN-DIFFERENT")]
  public async Task AttachTicket_with_conflicting_identifiers_is_rejected_and_writes_nothing(
    string bookingNo,
    string ticketNo
  )
  {
    // identifiers are facts about the captured KTMB reservation: repair may
    // backfill a missing one but never rewrite reservation identity
    var booking = BookingWith(BookStatus.Completed, "key", "BN-1", "TN-1");
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo)
      .AttachTicket(booking.Principal.Id, bookingNo, ticketNo, new MemoryStream([1]));

    result.IsSuccess().Should().BeFalse("a different non-null identifier is a conflict");
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0, "no write may happen when the guard fails");
  }

  [Theory]
  [InlineData(BookStatus.Pending)]
  [InlineData(BookStatus.Buying)]
  [InlineData(BookStatus.Cancelled)]
  [InlineData(BookStatus.Refunded)]
  [InlineData(BookStatus.Terminated)]
  [InlineData(BookStatus.Recovering)]
  [InlineData(BookStatus.Duplicate)]
  [InlineData(BookStatus.RequireManualIntervention)]
  public async Task AttachTicket_on_non_completed_booking_is_rejected_and_writes_nothing(
    BookStatus status
  )
  {
    // in-flight bookings go through Complete (which moves money); attach is
    // strictly a post-completion repair
    var booking = BookingWith(status, ticket: null, bookingNumber: null, ticketNumber: null);
    var repo = new FakeBookingRepository(booking);

    var result = await MakeService(repo)
      .AttachTicket(booking.Principal.Id, "BN-1", "TN-1", new MemoryStream([1]));

    result.IsSuccess().Should().BeFalse($"a '{status}' booking cannot have its ticket repaired");
    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    repo.UpdateCalls.Should().Be(0);
  }

  [Fact]
  public async Task AttachTicket_of_missing_booking_is_rejected()
  {
    var repo = new FakeBookingRepository(null);

    var result = await MakeService(repo).AttachTicket(Guid.NewGuid(), null, null, new MemoryStream([1]));

    result.IsSuccess().Should().BeFalse();
    repo.UpdateCalls.Should().Be(0);
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
      var principal = booking!.Principal with { Complete = complete! };
      return Task.FromResult((Result<BookingPrincipal?>)principal);
    }

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
