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

// The admin "check our KTMB account" request path. Invariants: it is strictly
// read-only for the booking (the fake booking repository throws on every
// mutator), an in-flight check is reused instead of queueing another KTMB
// scan, a failed enqueue never leaves a Pending check tin will never answer,
// and tin's first answer wins.
public class BookingKtmbCheckServiceTests
{
  private static readonly DateTimeOffset Now = new(2026, 10, 31, 3, 0, 0, TimeSpan.Zero);

  private static (BookingKtmbCheckService Service, FakeChecks Checks, FakeQueue Queue) Make(
    Booking? booking,
    bool queueFails = false
  )
  {
    var checks = new FakeChecks();
    var queue = new FakeQueue(queueFails);
    var service = new BookingKtmbCheckService(
      new ReadOnlyBookings(booking),
      checks,
      queue,
      NullLogger<BookingKtmbCheckService>.Instance,
      new FixedTimeProvider(Now)
    );
    return (service, checks, queue);
  }

  [Fact]
  public async Task Request_creates_a_pending_check_and_queues_ids_only()
  {
    var booking = BookingFixture();
    var (service, checks, queue) = Make(booking);

    var r = await service.Request(booking.Principal.Id, "admin-sub");

    var c = r.SuccessOrDefault()!;
    c.Status.Should().Be(KtmbCheckStatus.Pending);
    c.RequestedBy.Should().Be("admin-sub");
    c.RequestedAt.Should().Be(Now.UtcDateTime);
    queue.Sent.Should().ContainSingle()
      .Which.Should().Be(new BookingKtmbCheckRequest { CheckId = c.Id, BookingId = booking.Principal.Id });
    checks.Rows.Should().HaveCount(1);
  }

  [Fact]
  public async Task Request_for_missing_booking_returns_null_and_queues_nothing()
  {
    var (service, checks, queue) = Make(null);

    var r = await service.Request(Guid.NewGuid(), "admin-sub");

    r.IsSuccess().Should().BeTrue();
    r.SuccessOrDefault().Should().BeNull();
    queue.Sent.Should().BeEmpty();
    checks.Rows.Should().BeEmpty();
  }

  [Fact]
  public async Task Request_reuses_a_recent_in_flight_check()
  {
    var booking = BookingFixture();
    var (service, checks, queue) = Make(booking);
    var inFlight = checks.Seed(booking.Principal.Id, KtmbCheckStatus.Pending, Now.UtcDateTime.AddSeconds(-30));

    var r = await service.Request(booking.Principal.Id, "other-admin");

    r.SuccessOrDefault()!.Id.Should().Be(inFlight.Id);
    queue.Sent.Should().BeEmpty("one KTMB scan per in-flight check");
  }

  [Fact]
  public async Task Request_queues_a_new_check_when_the_pending_one_is_stale_or_answered()
  {
    var booking = BookingFixture();
    var (service, checks, queue) = Make(booking);
    checks.Seed(booking.Principal.Id, KtmbCheckStatus.Pending, Now.UtcDateTime.AddMinutes(-5));

    (await service.Request(booking.Principal.Id, "a")).IsSuccess().Should().BeTrue();
    queue.Sent.Should().HaveCount(1, "a Pending check older than the reuse window is presumed lost");

    var (service2, checks2, queue2) = Make(booking);
    checks2.Seed(booking.Principal.Id, KtmbCheckStatus.NotFound, Now.UtcDateTime.AddSeconds(-10));
    (await service2.Request(booking.Principal.Id, "a")).IsSuccess().Should().BeTrue();
    queue2.Sent.Should().HaveCount(1, "an answered check can always be re-run");
  }

  [Fact]
  public async Task Request_with_failed_enqueue_records_an_error_instead_of_a_dangling_pending()
  {
    var booking = BookingFixture();
    var (service, checks, _) = Make(booking, queueFails: true);

    var r = await service.Request(booking.Principal.Id, "admin-sub");

    var c = r.SuccessOrDefault()!;
    c.Status.Should().Be(KtmbCheckStatus.Error);
    c.Error.Should().NotBeNullOrEmpty();
    c.CheckedAt.Should().NotBeNull();
  }

  [Fact]
  public async Task Record_writes_the_first_answer_and_ignores_later_ones()
  {
    var booking = BookingFixture();
    var (service, checks, _) = Make(booking);
    var pending = checks.Seed(booking.Principal.Id, KtmbCheckStatus.Pending, Now.UtcDateTime);
    var claimant = Guid.NewGuid();

    var first = await service.Record(
      pending.Id,
      new BookingKtmbCheckResult
      {
        Status = KtmbCheckStatus.Found,
        KtmbBookingNo = "BN-1",
        KtmbTicketNo = "TN-1",
        ClaimedByBookingId = claimant,
      }
    );
    var second = await service.Record(
      pending.Id,
      new BookingKtmbCheckResult { Status = KtmbCheckStatus.Error, Error = "late" }
    );

    first.SuccessOrDefault()!.Status.Should().Be(KtmbCheckStatus.Found);
    first.SuccessOrDefault()!.ClaimedByBookingId.Should().Be(claimant);
    first.SuccessOrDefault()!.CheckedAt.Should().Be(Now.UtcDateTime);
    second.SuccessOrDefault()!.Status.Should().Be(KtmbCheckStatus.Found, "the first answer wins");
    checks.RecordCalls.Should().Be(1);
  }

  [Fact]
  public async Task Record_for_unknown_check_returns_null()
  {
    var (service, _, _) = Make(BookingFixture());

    var r = await service.Record(
      Guid.NewGuid(),
      new BookingKtmbCheckResult { Status = KtmbCheckStatus.NotFound }
    );

    r.IsSuccess().Should().BeTrue();
    r.SuccessOrDefault().Should().BeNull();
  }

  private static Booking BookingFixture() =>
    new()
    {
      Principal = new BookingPrincipal
      {
        Id = Guid.NewGuid(),
        UserId = "user-1",
        CreatedAt = Now.UtcDateTime,
        Record = new BookingRecord
        {
          Date = new DateOnly(2026, 11, 2),
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
        Status = new BookingStatus { Status = BookStatus.Recovering, CompletedAt = null },
        Complete = new BookingComplete
        {
          Ticket = null,
          BookingNumber = null,
          TicketNumber = null,
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
        CreatedAt = Now.UtcDateTime,
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

  private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => now;
  }

  private sealed class FakeQueue(bool fails) : IBookingKtmbCheckQueue
  {
    public List<BookingKtmbCheckRequest> Sent { get; } = [];

    public Task<Result<Unit>> Enqueue(BookingKtmbCheckRequest request)
    {
      if (fails)
        return Task.FromResult((Result<Unit>)new InvalidOperationException("redis down"));
      Sent.Add(request);
      return Task.FromResult((Result<Unit>)new Unit());
    }
  }

  private sealed class FakeChecks : IBookingKtmbCheckRepository
  {
    public List<BookingKtmbCheck> Rows { get; } = [];
    public int RecordCalls { get; private set; }

    public BookingKtmbCheck Seed(Guid bookingId, KtmbCheckStatus status, DateTime requestedAt)
    {
      var c = New(bookingId, null, requestedAt) with { Status = status };
      Rows.Add(c);
      return c;
    }

    private static BookingKtmbCheck New(Guid bookingId, string? by, DateTime at) =>
      new()
      {
        Id = Guid.NewGuid(),
        BookingId = bookingId,
        Status = KtmbCheckStatus.Pending,
        RequestedAt = at,
        RequestedBy = by,
        CheckedAt = null,
        KtmbBookingNo = null,
        KtmbTicketNo = null,
        ClaimedByBookingId = null,
        Error = null,
      };

    public Task<Result<BookingKtmbCheck>> Create(Guid bookingId, string? requestedBy, DateTime requestedAt)
    {
      var c = New(bookingId, requestedBy, requestedAt);
      Rows.Add(c);
      return Task.FromResult((Result<BookingKtmbCheck>)c);
    }

    public Task<Result<BookingKtmbCheck?>> Latest(Guid bookingId) =>
      Task.FromResult(
        (Result<BookingKtmbCheck?>)
          Rows.Where(x => x.BookingId == bookingId).MaxBy(x => x.RequestedAt)
      );

    public Task<Result<BookingKtmbCheck?>> Get(Guid checkId) =>
      Task.FromResult((Result<BookingKtmbCheck?>)Rows.FirstOrDefault(x => x.Id == checkId));

    public Task<Result<BookingKtmbCheck?>> Record(
      Guid checkId,
      BookingKtmbCheckResult result,
      DateTime checkedAt
    )
    {
      RecordCalls++;
      var i = Rows.FindIndex(x => x.Id == checkId);
      if (i < 0)
        return Task.FromResult((Result<BookingKtmbCheck?>)(BookingKtmbCheck?)null);
      Rows[i] = Rows[i] with
      {
        Status = result.Status,
        CheckedAt = checkedAt,
        KtmbBookingNo = result.KtmbBookingNo,
        KtmbTicketNo = result.KtmbTicketNo,
        ClaimedByBookingId = result.ClaimedByBookingId,
        Error = result.Error,
      };
      return Task.FromResult((Result<BookingKtmbCheck?>)Rows[i]);
    }
  }

  // Get is the ONLY booking read the check path may make; every mutator
  // throws, so a check that tried to change the booking would fail the test
  private sealed class ReadOnlyBookings(Booking? booking) : IBookingRepository
  {
    private static Exception Mutation() =>
      new InvalidOperationException("a KTMB check must never mutate a booking");

    public Task<Result<Booking?>> Get(string? userId, Guid id) =>
      Task.FromResult((Result<Booking?>)(booking?.Principal.Id == id ? booking : null));

    public Task<Result<BookingPrincipal?>> Update(
      string? userId,
      Guid id,
      BookingStatus? status,
      BookingRecord? record,
      BookingComplete? complete
    ) => throw Mutation();

    public Task<Result<BookingPrincipal?>> IncrementRecoveryRetries(Guid id) => throw Mutation();

    public Task<Result<BookingPrincipal>> Create(
      string userId,
      Guid transactionId,
      BookingRecord record,
      BookingPriceBreakdown? breakdown = null
    ) => throw Mutation();

    public Task<Result<BookingPrincipal?>> Prioritize(
      string? userId,
      Guid id,
      decimal? fee,
      string? grantedBy = null
    ) => throw Mutation();

    public Task<Result<Unit?>> Delete(string? userId, Guid id) => throw Mutation();

    public Task<Result<int>> WipePersonalData(string userId) => throw Mutation();

    public Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search) =>
      throw new NotImplementedException();

    public Task<Result<int>> SearchCount(BookingSearch search) => throw new NotImplementedException();

    public Task<Result<BookingQueuePosition?>> QueuePosition(string? userId, Guid id) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingStatRow>>> Stats(BookingStatsQuery query) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingPrincipal>>> RefundList(DateOnly date, TimeOnly time) =>
      throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Reserve(
      TrainDirection direction,
      DateOnly date,
      TimeOnly time
    ) => throw new NotImplementedException();

    public Task<Result<int>> CountSlotPriority(
      TrainDirection direction,
      DateOnly date,
      TimeOnly time
    ) => throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingKtmbCostMissing>>> ListMissingKtmbCost(
      BookStatus status,
      int limit,
      int skip
    ) => throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingKtmbCostMissing>>> ListMissingKtmbRefund(
      int limit,
      int skip
    ) => throw new NotImplementedException();

    public Task<Result<string[]>> ListTicketKeys(string userId) => throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingCount>>> Count(
      DateOnly date,
      TimeOnly time,
      DateOnly? filterDate,
      TrainDirection? filterDirection
    ) => throw new NotImplementedException();
  }
}
