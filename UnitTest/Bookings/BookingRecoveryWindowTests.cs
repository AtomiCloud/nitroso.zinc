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

// The duplicate-passport retry budget: every recycle must happen within
// MaxWindow of the booking's FIRST Recovering entry, at most MaxRetries times,
// and never when departure is closer than MinDepartureLead (tin's reserver
// would never buy it again). RecoverRevert refuses with the existing 409
// (RecoveryRetriesExhaustedException, now carrying the Reason) so the
// recoverer ends the booking as Duplicate; ExpireRecovery ends recycled
// bookings stuck in Pending the same way instead of leaving them for the
// post-departure refund.
public class BookingRecoveryWindowTests
{
  // fixture departure: 2026-07-03 08:00 SGT = 2026-07-03 00:00Z
  private static readonly DateOnly DepartDate = new(2026, 7, 3);
  private static readonly TimeOnly DepartTime = new(8, 0);
  private static readonly DateTimeOffset Departure = new(2026, 7, 3, 0, 0, 0, TimeSpan.Zero);

  // two days before departure
  private static readonly DateTimeOffset Now = Departure.AddDays(-2);

  private static readonly RecoveryPolicy Policy = new()
  {
    MaxRetries = 10,
    MaxWindow = TimeSpan.FromHours(2),
    MinDepartureLead = TimeSpan.FromHours(2),
  };

  private static Booking BookingWith(
    BookStatus status,
    DateTime? firstRecoveringAt,
    int retries = 0,
    DateOnly? date = null,
    TimeOnly? time = null,
    string? bookingNumber = null
  )
  {
    var id = Guid.NewGuid();
    return new Booking
    {
      Principal = new BookingPrincipal
      {
        Id = id,
        UserId = "user-1",
        CreatedAt = Now.UtcDateTime.AddDays(-1),
        Record = new BookingRecord
        {
          Date = date ?? DepartDate,
          Time = time ?? DepartTime,
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
          FirstRecoveringAt = firstRecoveringAt,
        },
        Complete = new BookingComplete
        {
          Ticket = null,
          BookingNumber = bookingNumber,
          TicketNumber = null,
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
        CreatedAt = Now.UtcDateTime.AddDays(-1),
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

  // a departure `lead` after Now, as the SGT wall-clock Date/Time zinc stores
  private static (DateOnly Date, TimeOnly Time) DepartIn(TimeSpan lead)
  {
    var sgt = Now.UtcDateTime.Add(lead).AddHours(8);
    return (DateOnly.FromDateTime(sgt), TimeOnly.FromDateTime(sgt));
  }

  // ---- RecoveryPolicy (pure) ----

  [Fact]
  public void Policy_inside_window_with_budget_is_not_exhausted()
  {
    var b = BookingWith(BookStatus.Recovering, Now.UtcDateTime.AddHours(-1), retries: 3);
    Policy.Exhausted(b.Principal, Now, countRetries: true).Should().BeNull();
  }

  [Fact]
  public void Policy_window_edge_is_exclusive_of_the_full_window()
  {
    var justInside = BookingWith(
      BookStatus.Recovering,
      Now.UtcDateTime.AddHours(-2).AddSeconds(1)
    );
    var atWindow = BookingWith(BookStatus.Recovering, Now.UtcDateTime.AddHours(-2));

    Policy.Exhausted(justInside.Principal, Now, countRetries: true).Should().BeNull();
    Policy
      .Exhausted(atWindow.Principal, Now, countRetries: true)
      .Should()
      .Be(RecoveryExhaustion.Window, "2h after the first rejection the window is over");
  }

  [Fact]
  public void Policy_cap_applies_only_when_counting_retries()
  {
    var b = BookingWith(BookStatus.Recovering, Now.UtcDateTime.AddMinutes(-30), retries: 10);

    Policy.Exhausted(b.Principal, Now, countRetries: true).Should().Be(RecoveryExhaustion.Retries);
    Policy
      .Exhausted(b.Principal, Now, countRetries: false)
      .Should()
      .BeNull("the Pending expiry lets the last allowed recycle run until the clock ends it");
  }

  [Fact]
  public void Policy_without_first_stamp_never_guesses_a_window()
  {
    var legacy = BookingWith(BookStatus.Recovering, firstRecoveringAt: null);
    Policy.Exhausted(legacy.Principal, Now, countRetries: true).Should().BeNull();
  }

  [Fact]
  public void Policy_departure_edge_allows_exactly_the_lead_and_refuses_closer()
  {
    var (d1, t1) = DepartIn(TimeSpan.FromHours(2));
    var (d2, t2) = DepartIn(TimeSpan.FromHours(2).Subtract(TimeSpan.FromMinutes(1)));
    var exactly = BookingWith(BookStatus.Recovering, Now.UtcDateTime, date: d1, time: t1);
    var closer = BookingWith(BookStatus.Recovering, Now.UtcDateTime, date: d2, time: t2);

    Policy.Exhausted(exactly.Principal, Now, countRetries: true).Should().BeNull();
    Policy
      .Exhausted(closer.Principal, Now, countRetries: true)
      .Should()
      .Be(RecoveryExhaustion.Departure, "tin cannot buy it again before it departs");
  }

  [Fact]
  public void Policy_departure_inside_remaining_window_ends_early()
  {
    // first rejection 10 min ago, so 1h50 of window remain — but departure is
    // only 1h30 away: no point retrying past departure
    var (d, t) = DepartIn(TimeSpan.FromMinutes(90));
    var b = BookingWith(BookStatus.Recovering, Now.UtcDateTime.AddMinutes(-10), date: d, time: t);

    Policy.Exhausted(b.Principal, Now, countRetries: true).Should().Be(RecoveryExhaustion.Departure);
  }

  [Fact]
  public void Policy_departed_booking_is_exhausted()
  {
    var (d, t) = DepartIn(TimeSpan.FromHours(-1));
    var b = BookingWith(BookStatus.Recovering, Now.UtcDateTime.AddMinutes(-10), date: d, time: t);

    Policy.Exhausted(b.Principal, Now, countRetries: true).Should().Be(RecoveryExhaustion.Departure);
  }

  // ---- RecoverRevert ----

  [Fact]
  public async Task RecoverRevert_past_window_is_refused_with_window_reason_and_writes_nothing()
  {
    var booking = BookingWith(BookStatus.Recovering, Now.UtcDateTime.AddHours(-2), retries: 4);
    var h = Harness.For(booking);

    var r = await h.Service.RecoverRevert(booking.Principal.Id, Policy);

    var e = r.FailureOrDefault().Should().BeOfType<RecoveryRetriesExhaustedException>().Subject;
    e.Reason.Should().Be(RecoveryExhaustion.Window);
    e.Retries.Should().Be(4);
    h.Repo.IncrementCalls.Should().Be(0);
    h.Repo.StatusWrites.Should().BeEmpty("an exhausted booking stays Recovering for markDuplicate");
    h.Cdc.Actions.Should().BeEmpty();
  }

  [Fact]
  public async Task RecoverRevert_too_close_to_departure_is_refused_with_departure_reason()
  {
    var (d, t) = DepartIn(TimeSpan.FromMinutes(100));
    var booking = BookingWith(BookStatus.Recovering, Now.UtcDateTime.AddMinutes(-5), date: d, time: t);
    var h = Harness.For(booking);

    var r = await h.Service.RecoverRevert(booking.Principal.Id, Policy);

    r.FailureOrDefault()
      .Should()
      .BeOfType<RecoveryRetriesExhaustedException>()
      .Which.Reason.Should()
      .Be(RecoveryExhaustion.Departure);
    h.Repo.StatusWrites.Should().BeEmpty();
  }

  [Fact]
  public async Task RecoverRevert_inside_window_recycles_and_fires_cdc()
  {
    var booking = BookingWith(BookStatus.Recovering, Now.UtcDateTime.AddMinutes(-50), retries: 2);
    var h = Harness.For(booking);

    var r = await h.Service.RecoverRevert(booking.Principal.Id, Policy);

    r.IsSuccess().Should().BeTrue();
    h.Repo.IncrementCalls.Should().Be(1);
    h.Repo.StatusWrites.Should().Equal(BookStatus.Pending);
    h.Cdc.Actions.Should()
      .Equal(["update"], "tin's demand counts must see the recycled booking right away");
  }

  // ---- ExpireRecovery ----

  [Fact]
  public async Task ExpireRecovery_ends_only_recycled_pending_bookings_past_their_budget()
  {
    var (nd, nt) = DepartIn(TimeSpan.FromMinutes(60));
    var pastWindow = BookingWith(BookStatus.Pending, Now.UtcDateTime.AddMinutes(-121), retries: 3);
    var nearDeparture = BookingWith(BookStatus.Pending, Now.UtcDateTime.AddMinutes(-20), date: nd, time: nt);
    var fresh = BookingWith(BookStatus.Pending, Now.UtcDateTime.AddMinutes(-20), retries: 2);
    var lastRetryInFlight = BookingWith(BookStatus.Pending, Now.UtcDateTime.AddMinutes(-90), retries: 10);
    var h = Harness.For(pastWindow, nearDeparture, fresh, lastRetryInFlight);

    var r = await h.Service.ExpireRecovery(Policy);

    r.IsSuccess().Should().BeTrue();
    r.SuccessOrDefault()
      .Select(x => x.Id)
      .Should()
      .BeEquivalentTo([pastWindow.Principal.Id, nearDeparture.Principal.Id]);
    h.Repo.LastSearch!.Status.Should().Be(BookStatus.Pending);
    h.Repo.LastSearch!.Recycled.Should().BeTrue();
    h.Repo.StatusOf(pastWindow.Principal.Id).Should().Be(BookStatus.Duplicate);
    h.Repo.StatusOf(nearDeparture.Principal.Id).Should().Be(BookStatus.Duplicate);
    h.Repo.StatusOf(fresh.Principal.Id).Should().Be(BookStatus.Pending);
    h.Repo.StatusOf(lastRetryInFlight.Principal.Id).Should().Be(BookStatus.Pending);
    h.Wallet.BookEndReverts.Should().Equal([26.0m, 26.0m], "the full reserve is refunded, as markDuplicate does");
    h.Txn.Records.Should().HaveCount(2);
  }

  [Fact]
  public async Task ExpireRecovery_walks_every_page_so_the_soonest_departure_is_not_starved()
  {
    // newest-departure-first order puts the urgent booking on the last page
    var later = Enumerable
      .Range(1, 5)
      .Select(i =>
      {
        var (d, t) = DepartIn(TimeSpan.FromDays(10 + i));
        return BookingWith(BookStatus.Pending, Now.UtcDateTime.AddMinutes(-10), date: d, time: t);
      })
      .ToArray();
    var (ud, ut) = DepartIn(TimeSpan.FromMinutes(90));
    var urgent = BookingWith(BookStatus.Pending, Now.UtcDateTime.AddMinutes(-10), date: ud, time: ut);
    var stale = BookingWith(BookStatus.Pending, Now.UtcDateTime.AddHours(-3), date: DepartDate);
    var h = Harness.ForPaged(2, [.. later, stale, urgent]);

    var r = await h.Service.ExpireRecovery(Policy);

    r.SuccessOrDefault()
      .Select(x => x.Id)
      .Should()
      .BeEquivalentTo([urgent.Principal.Id, stale.Principal.Id]);
    later.Should().OnlyContain(b => h.Repo.StatusOf(b.Principal.Id) == BookStatus.Pending);
  }

  [Fact]
  public async Task ExpireRecovery_skips_a_booking_the_reserver_picked_up_meanwhile()
  {
    var booking = BookingWith(BookStatus.Pending, Now.UtcDateTime.AddHours(-3));
    var h = Harness.For(booking);
    // the search saw it Pending, but by the time the Duplicate transaction
    // re-reads it the buyer has moved it to Buying
    h.Repo.FlipToOnGet[booking.Principal.Id] = BookStatus.Buying;

    var r = await h.Service.ExpireRecovery(Policy);

    r.IsSuccess().Should().BeTrue();
    r.SuccessOrDefault().Should().BeEmpty();
    h.Repo.StatusOf(booking.Principal.Id).Should().Be(BookStatus.Buying);
    h.Wallet.BookEndReverts.Should().BeEmpty("no refund under a live purchase");
  }

  [Fact]
  public async Task ExpireRecovery_never_refunds_a_captured_booking()
  {
    var booking = BookingWith(BookStatus.Pending, Now.UtcDateTime.AddHours(-3), bookingNumber: "BN-1");
    var h = Harness.For(booking);

    var r = await h.Service.ExpireRecovery(Policy);

    r.SuccessOrDefault().Should().BeEmpty();
    h.Wallet.BookEndReverts.Should().BeEmpty();
  }

  // ---- harness ----

  private sealed record Harness(
    BookingService Service,
    FakeRepo Repo,
    FakeCdc Cdc,
    FakeWallet Wallet,
    FakeTxn Txn
  )
  {
    public static Harness For(params Booking[] bookings) => ForPaged(500, bookings);

    public static Harness ForPaged(int pageSize, params Booking[] bookings)
    {
      var repo = new FakeRepo(bookings);
      var cdc = new FakeCdc();
      var wallet = new FakeWallet();
      var txn = new FakeTxn();
      var service = new BookingService(
        repo,
        null!,
        wallet,
        txn,
        new PassThroughTransactionManager(),
        new TransactionGenerator(),
        null!,
        null!,
        cdc,
        new FakeNotifier(),
        null!,
        null!,
        null!,
        NullLogger<BookingService>.Instance,
        new FixedTimeProvider(Now)
      )
      {
        ExpirePageSize = pageSize,
      };
      return new Harness(service, repo, cdc, wallet, txn);
    }
  }

  private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
  {
    public override DateTimeOffset GetUtcNow() => now;
  }

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FakeCdc : IBookingCdcRepository
  {
    public List<string> Actions { get; } = [];

    public Task<Result<Unit>> Add(string action)
    {
      Actions.Add(action);
      return Task.FromResult((Result<Unit>)new Unit());
    }
  }

  private sealed class FakeNotifier : IBookingNotificationService
  {
    private static Task<Result<Unit>> Ok() => Task.FromResult((Result<Unit>)new Unit());

    public Task<Result<Unit>> NotifyBookingCompleted(Booking booking) => Ok();

    public Task<Result<Unit>> NotifyBookingCancelled(Booking booking) => Ok();

    public Task<Result<Unit>> NotifyBookingTerminated(Booking booking) => Ok();

    public Task<Result<Unit>> NotifyBookingRefunded(Booking booking) => Ok();

    public Task<Result<Unit>> NotifyBookingDuplicate(Booking booking) => Ok();

    public Task<Result<Unit>> NotifyBookingManualIntervention(Booking booking) => Ok();
  }

  private sealed class FakeWallet : IWalletRepository
  {
    public List<decimal> BookEndReverts { get; } = [];

    private static WalletPrincipal W(Guid id) =>
      new()
      {
        Id = id,
        UserId = "user-1",
        Record = new WalletRecord { Usable = 100m, WithdrawReserve = 0m, BookingReserve = 0m },
      };

    public Task<Result<WalletPrincipal?>> BookEnd(Guid id, decimal revert, decimal collect)
    {
      BookEndReverts.Add(revert);
      return Task.FromResult((Result<WalletPrincipal?>)W(id));
    }

    public Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount) =>
      Task.FromResult((Result<WalletPrincipal?>)W(id));

    public Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Wallet?>> Get(Guid id, string? userId) => throw new NotImplementedException();

    public Task<Result<Wallet?>> GetByUserId(string userId) => throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> PrepareWithdraw(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Withdraw(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> CancelWithdraw(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> BookStart(Guid id, decimal amount) =>
      throw new NotImplementedException();
  }

  private sealed class FakeTxn : ITransactionRepository
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

  // stateful multi-booking fake: Search mimics the Pending + Recycled filter,
  // FlipToOnGet simulates a concurrent transition seen inside the transaction
  private sealed class FakeRepo(IEnumerable<Booking> seed) : IBookingRepository
  {
    private readonly Dictionary<Guid, Booking> bookings = seed.ToDictionary(b => b.Principal.Id);

    public Dictionary<Guid, BookStatus> FlipToOnGet { get; } = [];
    public List<BookStatus> StatusWrites { get; } = [];
    public int IncrementCalls { get; private set; }
    public BookingSearch? LastSearch { get; private set; }

    public BookStatus StatusOf(Guid id) => bookings[id].Principal.Status.Status;

    private void Set(Guid id, Func<BookingPrincipal, BookingPrincipal> f) =>
      bookings[id] = bookings[id] with { Principal = f(bookings[id].Principal) };

    public Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search)
    {
      LastSearch = search;
      var rows = bookings
        .Values.Select(b => b.Principal)
        .Where(p => search.Status == null || p.Status.Status == search.Status)
        .Where(p => search.Recycled != true || p.Status.FirstRecoveringAt != null)
        // the real default order: travel date descending
        .OrderByDescending(p => p.Record.Date.ToDateTime(p.Record.Time))
        .Skip(search.Skip)
        .Take(search.Limit > 0 ? search.Limit : int.MaxValue)
        .ToArray();
      return Task.FromResult((Result<IEnumerable<BookingPrincipal>>)rows);
    }

    public Task<Result<Booking?>> Get(string? userId, Guid id)
    {
      if (FlipToOnGet.Remove(id, out var flip))
        Set(id, p => p with { Status = p.Status with { Status = flip } });
      return Task.FromResult((Result<Booking?>)(bookings.TryGetValue(id, out var b) ? b : null));
    }

    public Task<Result<BookingPrincipal?>> Update(
      string? userId,
      Guid id,
      BookingStatus? status,
      BookingRecord? record,
      BookingComplete? complete
    )
    {
      StatusWrites.Add(status!.Status);
      Set(id, p => p with { Status = p.Status with { Status = status.Status } });
      return Task.FromResult((Result<BookingPrincipal?>)bookings[id].Principal);
    }

    public Task<Result<BookingPrincipal?>> IncrementRecoveryRetries(Guid id)
    {
      IncrementCalls++;
      Set(id, p => p with { RecoveryRetries = p.RecoveryRetries + 1 });
      return Task.FromResult((Result<BookingPrincipal?>)bookings[id].Principal);
    }

    public Task<Result<int>> SearchCount(BookingSearch search) => throw new NotImplementedException();

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
      BookingPriceBreakdown? breakdown = null
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
    ) => throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Prioritize(
      string? userId,
      Guid id,
      decimal? fee,
      string? grantedBy = null
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

    public Task<Result<Unit?>> Delete(string? userId, Guid id) => throw new NotImplementedException();

    public Task<Result<string[]>> ListTicketKeys(string userId) => throw new NotImplementedException();

    public Task<Result<int>> WipePersonalData(string userId) => throw new NotImplementedException();

    public Task<Result<IEnumerable<BookingCount>>> Count(
      DateOnly date,
      TimeOnly time,
      DateOnly? filterDate,
      TrainDirection? filterDirection
    ) => throw new NotImplementedException();
  }
}
