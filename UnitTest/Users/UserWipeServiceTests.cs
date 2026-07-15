using CSharp_Result;
using Domain;
using Domain.Booking;
using Domain.Exceptions;
using Domain.Passenger;
using Domain.Timings;
using Domain.User;
using Domain.Wallet;
using Domain.Withdrawal;

namespace UnitTest.Users;

// PDPA account wipe guards + erasure/retention split on UserWipeService.
// The invariants: nothing is mutated until every guard passes, ticket blobs
// are removed BEFORE the DB scrub (retryable in both directions), and the
// financial repos (wallet, withdrawal, transaction) are never written —
// every fake below throws on any call the wipe must not make.
public class UserWipeServiceTests
{
  private const string UserId = "user-1234567890";
  private const string AdminId = "admin-1";

  private static readonly DateTime WipeStamp = new(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc);

  private static User UserWith(
    decimal usable = 0m,
    decimal withdrawReserve = 0m,
    decimal bookingReserve = 0m,
    DateTime? wipedAt = null
  ) =>
    new()
    {
      Principal = new UserPrincipal
      {
        Id = UserId,
        Record = new UserRecord { Username = "bunny", Email = "bunny@example.com" },
        WipedAt = wipedAt,
      },
      Wallet = new WalletPrincipal
      {
        Id = Guid.NewGuid(),
        UserId = UserId,
        Record = new WalletRecord
        {
          Usable = usable,
          WithdrawReserve = withdrawReserve,
          BookingReserve = bookingReserve,
        },
      },
    };

  private static (
    UserWipeService Service,
    FakeUserRepository Users,
    FakePassengerRepository Passengers,
    FakeBookingRepository Bookings,
    FakeBookingStorage Storage,
    FakeWithdrawalRepository Withdrawals,
    List<string> Calls
  ) Make(
    User? user,
    string[]? ticketKeys = null,
    WithdrawStatus[]? inFlight = null,
    bool storageFails = false
  )
  {
    var calls = new List<string>();
    var users = new FakeUserRepository(user, calls);
    var passengers = new FakePassengerRepository(calls);
    var bookings = new FakeBookingRepository(ticketKeys ?? [], calls);
    var storage = new FakeBookingStorage(storageFails, calls);
    var withdrawals = new FakeWithdrawalRepository(inFlight ?? [], calls);
    var service = new UserWipeService(
      users,
      passengers,
      bookings,
      storage,
      withdrawals,
      new PassThroughTransactionManager()
    );
    return (service, users, passengers, bookings, storage, withdrawals, calls);
  }

  // ---- guard refusals -----------------------------------------------------

  [Fact]
  public async Task Wipe_of_unknown_user_returns_null_and_touches_nothing()
  {
    var (service, _, passengers, bookings, storage, _, _) = Make(user: null);

    var result = await service.Wipe(UserId, AdminId);

    result.IsSuccess().Should().BeTrue();
    result.Get().Should().BeNull();
    passengers.DeletedUsers.Should().BeEmpty();
    bookings.WipedUsers.Should().BeEmpty();
    storage.RemovedKeys.Should().BeEmpty();
  }

  [Fact]
  public async Task Wipe_refuses_an_already_wiped_user()
  {
    var (service, users, passengers, bookings, storage, _, _) = Make(
      UserWith(wipedAt: WipeStamp)
    );

    var result = await service.Wipe(UserId, AdminId);

    var e = result
      .FailureOrDefault()
      .Should()
      .BeOfType<InvalidUserWipeOperationException>()
      .Subject;
    e.Reason.Should().Be("already_wiped");
    e.UserId.Should().Be(UserId);
    users.WipeCalls.Should().BeEmpty();
    passengers.DeletedUsers.Should().BeEmpty();
    bookings.WipedUsers.Should().BeEmpty();
    storage.RemovedKeys.Should().BeEmpty();
  }

  [Theory]
  [InlineData(10.5, 0, 0)]
  [InlineData(0, 25, 0)]
  [InlineData(0, 0, 31)]
  [InlineData(0.00000001, 0, 0)]
  public async Task Wipe_refuses_while_the_wallet_still_holds_money(
    decimal usable,
    decimal withdrawReserve,
    decimal bookingReserve
  )
  {
    var (service, users, passengers, bookings, storage, _, _) = Make(
      UserWith(usable, withdrawReserve, bookingReserve)
    );

    var result = await service.Wipe(UserId, AdminId);

    var e = result
      .FailureOrDefault()
      .Should()
      .BeOfType<InvalidUserWipeOperationException>()
      .Subject;
    e.Reason.Should().Be("wallet_not_empty");
    users.WipeCalls.Should().BeEmpty();
    passengers.DeletedUsers.Should().BeEmpty();
    bookings.WipedUsers.Should().BeEmpty();
    storage.RemovedKeys.Should().BeEmpty();
  }

  [Theory]
  [InlineData(WithdrawStatus.Pending)]
  [InlineData(WithdrawStatus.Processing)]
  [InlineData(WithdrawStatus.RequireManualIntervention)]
  public async Task Wipe_refuses_while_a_withdrawal_is_in_flight(WithdrawStatus status)
  {
    var (service, users, passengers, bookings, storage, _, _) = Make(
      UserWith(),
      inFlight: [status]
    );

    var result = await service.Wipe(UserId, AdminId);

    var e = result
      .FailureOrDefault()
      .Should()
      .BeOfType<InvalidUserWipeOperationException>()
      .Subject;
    e.Reason.Should().Be("withdrawal_in_flight");
    users.WipeCalls.Should().BeEmpty();
    passengers.DeletedUsers.Should().BeEmpty();
    bookings.WipedUsers.Should().BeEmpty();
    storage.RemovedKeys.Should().BeEmpty();
  }

  [Theory]
  [InlineData(WithdrawStatus.Completed)]
  [InlineData(WithdrawStatus.Rejected)]
  [InlineData(WithdrawStatus.Cancel)]
  public async Task Wipe_proceeds_when_only_settled_withdrawals_exist(WithdrawStatus status)
  {
    // settled payouts are financial history, not in-flight money — the fake
    // only reports rows for the exact status searched, so a settled-only
    // history must not block
    var (service, users, _, _, _, _, _) = Make(UserWith(), inFlight: [status]);

    var result = await service.Wipe(UserId, AdminId);

    result.IsSuccess().Should().BeTrue();
    users.WipeCalls.Should().ContainSingle();
  }

  // ---- the wipe itself ----------------------------------------------------

  [Fact]
  public async Task Wipe_deletes_passengers_scrubs_bookings_and_anonymizes_the_user()
  {
    var (service, users, passengers, bookings, storage, _, _) = Make(
      UserWith(),
      ticketKeys: ["bookings/t1.pdf", "bookings/t2.pdf"]
    );

    var result = await service.Wipe(UserId, AdminId);

    result.IsSuccess().Should().BeTrue();
    var wipe = result.Get();
    wipe.Should().NotBeNull();
    wipe!.Id.Should().Be(UserId);
    wipe.WipedAt.Should().Be(WipeStamp);

    passengers.DeletedUsers.Should().Equal(UserId);
    bookings.WipedUsers.Should().Equal(UserId);
    storage.RemovedKeys.Should().Equal("bookings/t1.pdf", "bookings/t2.pdf");
    users.WipeCalls.Should().Equal((UserId, AdminId));
  }

  [Fact]
  public async Task Wipe_removes_ticket_blobs_before_any_db_scrub()
  {
    var (service, _, _, _, _, _, calls) = Make(UserWith(), ticketKeys: ["bookings/t1.pdf"]);

    await service.Wipe(UserId, AdminId);

    var removeAt = calls.IndexOf("storage.Remove:bookings/t1.pdf");
    removeAt.Should().BeGreaterThanOrEqualTo(0);
    calls.IndexOf($"passengers.DeleteByUser:{UserId}").Should().BeGreaterThan(removeAt);
    calls.IndexOf($"bookings.WipePersonalData:{UserId}").Should().BeGreaterThan(removeAt);
    calls.IndexOf($"users.Wipe:{UserId}").Should().BeGreaterThan(removeAt);
  }

  [Fact]
  public async Task Wipe_aborts_without_db_changes_when_a_blob_removal_fails()
  {
    var (service, users, passengers, bookings, _, _, _) = Make(
      UserWith(),
      ticketKeys: ["bookings/t1.pdf"],
      storageFails: true
    );

    var result = await service.Wipe(UserId, AdminId);

    result.IsFailure().Should().BeTrue();
    passengers.DeletedUsers.Should().BeEmpty();
    bookings.WipedUsers.Should().BeEmpty();
    users.WipeCalls.Should().BeEmpty();
  }

  // ---- anonymization shape ------------------------------------------------

  [Fact]
  public void Anonymized_username_is_deleted_plus_first_8_chars_of_the_id()
  {
    UserWipeService.AnonymizedUsername("abcdefgh-rest-of-sub").Should().Be("deleted-abcdefgh");
  }

  [Fact]
  public void Anonymized_username_uses_the_whole_id_when_shorter_than_8()
  {
    UserWipeService.AnonymizedUsername("ab12").Should().Be("deleted-ab12");
  }

  // ---- fakes ----------------------------------------------------------

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FakeUserRepository(User? user, List<string> calls) : IUserRepository
  {
    public List<(string Id, string WipedById)> WipeCalls { get; } = [];

    public Task<Result<User?>> GetById(string id) => Task.FromResult((Result<User?>)user);

    public Task<Result<UserPrincipal?>> Wipe(string id, string wipedById)
    {
      calls.Add($"users.Wipe:{id}");
      this.WipeCalls.Add((id, wipedById));
      return Task.FromResult(
        (Result<UserPrincipal?>)
          new UserPrincipal
          {
            Id = id,
            Record = new UserRecord
            {
              Username = UserWipeService.AnonymizedUsername(id),
              Email = "",
              EmailVerified = false,
            },
            WipedAt = WipeStamp,
            WipedById = wipedById,
          }
      );
    }

    public Task<Result<IEnumerable<UserPrincipal>>> Search(UserSearch search) =>
      throw new NotImplementedException();

    public Task<Result<User?>> GetByUsername(string username) =>
      throw new NotImplementedException();

    public Task<Result<bool>> Exists(string username) => throw new NotImplementedException();

    public Task<Result<UserPrincipal>> Create(string id, UserRecord record) =>
      throw new NotImplementedException();

    public Task<Result<UserPrincipal?>> Update(string id, UserRecord record) =>
      throw new NotImplementedException();

    public Task<Result<UserPrincipal?>> AddExtraRole(string id, string role) =>
      throw new NotImplementedException();

    public Task<Result<UserPrincipal?>> RemoveExtraRole(string id, string role) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(string id) => throw new NotImplementedException();
  }

  private sealed class FakePassengerRepository(List<string> calls) : IPassengerRepository
  {
    public List<string> DeletedUsers { get; } = [];

    public Task<Result<int>> DeleteByUser(string userId)
    {
      calls.Add($"passengers.DeleteByUser:{userId}");
      this.DeletedUsers.Add(userId);
      return Task.FromResult((Result<int>)2);
    }

    public Task<Result<IEnumerable<PassengerPrincipal>>> Search(PassengerSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Passenger?>> Get(string? userId, Guid id) =>
      throw new NotImplementedException();

    public Task<Result<PassengerPrincipal>> Create(string userId, PassengerRecord record) =>
      throw new NotImplementedException();

    public Task<Result<PassengerPrincipal?>> Update(
      string? userId,
      Guid id,
      PassengerRecord record
    ) => throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(string? userId, Guid id) =>
      throw new NotImplementedException();
  }

  private sealed class FakeBookingStorage(bool fails, List<string> calls) : IBookingStorage
  {
    public List<string> RemovedKeys { get; } = [];

    public Task<Result<Unit>> Remove(string key)
    {
      if (fails)
        return Task.FromResult((Result<Unit>)new ApplicationException("storage down"));
      calls.Add($"storage.Remove:{key}");
      this.RemovedKeys.Add(key);
      return Task.FromResult((Result<Unit>)new Unit());
    }

    public Task<Result<string>> Save(Stream stream) => throw new NotImplementedException();

    public Task<Result<string>> Get(string key) => throw new NotImplementedException();

    public Task<Result<bool>> Exists(string key) => throw new NotImplementedException();
  }

  private sealed class FakeWithdrawalRepository(WithdrawStatus[] inFlight, List<string> calls)
    : IWithdrawalRepository
  {
    public Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search)
    {
      calls.Add($"withdrawals.Search:{search.Status}");
      // report a row only for the exact status searched — the wipe probes
      // each in-flight status with Limit 1
      IEnumerable<WithdrawalPrincipal> rows =
        search.Status != null && inFlight.Contains(search.Status.Value)
          ? [Principal(search.Status.Value)]
          : [];
      return Task.FromResult((Result<IEnumerable<WithdrawalPrincipal>>)rows.ToResult());
    }

    private static WithdrawalPrincipal Principal(WithdrawStatus status) =>
      new()
      {
        Id = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        Status = new WithdrawalStatus { Status = status },
        Record = new WithdrawalRecord
        {
          Amount = 10m,
          Method = WithdrawalMethod.PayNow,
          PayNowNumber = "91234567",
        },
        Complete = null,
        Payout = null,
      };

    public Task<Result<Withdrawal?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<WithdrawalPrincipal>> Create(Guid walletId, WithdrawalRecord record) =>
      throw new NotImplementedException();

    public Task<Result<WithdrawalPrincipal?>> Update(
      string? userId,
      Guid id,
      WithdrawalRecord? record,
      WithdrawalStatus? status,
      WithdrawalComplete? complete,
      WithdrawalPayout? payout = null
    ) => throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }

  private sealed class FakeBookingRepository(string[] ticketKeys, List<string> calls)
    : IBookingRepository
  {
    public List<string> WipedUsers { get; } = [];

    public Task<Result<string[]>> ListTicketKeys(string userId)
    {
      calls.Add($"bookings.ListTicketKeys:{userId}");
      return Task.FromResult((Result<string[]>)ticketKeys);
    }

    public Task<Result<int>> WipePersonalData(string userId)
    {
      calls.Add($"bookings.WipePersonalData:{userId}");
      this.WipedUsers.Add(userId);
      return Task.FromResult((Result<int>)ticketKeys.Length);
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

    public Task<Result<Booking?>> Get(string? userId, Guid id) =>
      throw new NotImplementedException();

    public Task<Result<BookingPrincipal>> Create(
      string userId,
      Guid transactionId,
      BookingRecord record,
      BookingPriceBreakdown? breakdown
    ) => throw new NotImplementedException();

    public Task<Result<BookingPrincipal?>> Update(
      string? userId,
      Guid id,
      BookingStatus? status,
      BookingRecord? record,
      BookingComplete? complete
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
      string? grantedBy
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
