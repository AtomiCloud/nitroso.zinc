using CSharp_Result;
using Domain;
using Domain.Booking;
using Domain.Passenger;
using Domain.Timings;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;
using FluentAssertions;

namespace UnitTest.Bookings;

// TicketHealth is the cheap dangling-ref probe: HasRef = the booking carries a
// Ticket key, RefValid = the key resolves to a real stored object. It must
// never touch storage when there is no key, and a missing booking is null
// (404), not a health report.
public class BookingServiceTicketHealthTests
{
  private static BookingService MakeService(FakeBookingRepository repo, FakeStorage storage) =>
    new(
      repo,
      storage,
      null!,
      null!,
      null!,
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

  private static Booking BookingWith(string? ticket)
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
        Status = new BookingStatus { Status = BookStatus.Completed, CompletedAt = DateTime.UtcNow },
        Complete = new BookingComplete
        {
          Ticket = ticket,
          BookingNumber = "BN-1",
          TicketNumber = "TN-1",
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
  public async Task TicketHealth_with_valid_ref_reports_valid()
  {
    var booking = BookingWith("ticket-key");
    var storage = new FakeStorage(exists: true);
    var result = await MakeService(new FakeBookingRepository(booking), storage)
      .TicketHealth(booking.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    var h = result.Get()!;
    h.HasRef.Should().BeTrue();
    h.RefValid.Should().BeTrue();
    storage.ProbedKeys.Should().Equal("ticket-key");
  }

  [Fact]
  public async Task TicketHealth_with_dangling_ref_reports_invalid()
  {
    var booking = BookingWith("lost-key");
    var storage = new FakeStorage(exists: false);
    var result = await MakeService(new FakeBookingRepository(booking), storage)
      .TicketHealth(booking.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    var h = result.Get()!;
    h.HasRef.Should().BeTrue();
    h.RefValid.Should().BeFalse("the reference dangles — no stored object behind the key");
  }

  [Fact]
  public async Task TicketHealth_without_ref_skips_storage()
  {
    var booking = BookingWith(ticket: null);
    var storage = new FakeStorage(exists: true);
    var result = await MakeService(new FakeBookingRepository(booking), storage)
      .TicketHealth(booking.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    var h = result.Get()!;
    h.HasRef.Should().BeFalse();
    h.RefValid.Should().BeFalse();
    storage.ProbedKeys.Should().BeEmpty("no key means nothing to probe");
  }

  [Fact]
  public async Task TicketHealth_of_missing_booking_is_null()
  {
    var storage = new FakeStorage(exists: true);
    var result = await MakeService(new FakeBookingRepository(null), storage)
      .TicketHealth(Guid.NewGuid());

    result.IsSuccess().Should().BeTrue();
    result.Get().Should().BeNull("a missing booking maps to 404, not a health report");
    storage.ProbedKeys.Should().BeEmpty();
  }

  private sealed class FakeStorage(bool exists) : IBookingStorage
  {
    public List<string> ProbedKeys { get; } = [];

    public Task<Result<string>> Save(Stream stream) => throw new NotImplementedException();

    public Task<Result<string>> Get(string key) => throw new NotImplementedException();

    public Task<Result<bool>> Exists(string key)
    {
      ProbedKeys.Add(key);
      return Task.FromResult((Result<bool>)exists);
    }
  }

  private sealed class FakeBookingRepository(Booking? booking) : IBookingRepository
  {
    public Task<Result<Booking?>> Get(string? userId, Guid id) =>
      Task.FromResult((Result<Booking?>)booking);

    public Task<Result<BookingPrincipal?>> Update(
      string? userId,
      Guid id,
      BookingStatus? status,
      BookingRecord? record,
      BookingComplete? complete
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
