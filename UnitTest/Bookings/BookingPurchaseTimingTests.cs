using System.Reflection;
using Domain;
using Domain.Booking;
using Domain.Exceptions;
using Domain.Passenger;
using Domain.Timings;
using Domain.Transaction;
using Domain.Wallet;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Bookings;

public class BookingPurchaseTimingTests
{
  private static readonly DateOnly DepartureDate = new(2026, 7, 15);
  private static readonly TimeOnly DepartureTime = new(8, 30);

  private static BookingRecord Record(DateOnly date, TimeOnly time) =>
    new()
    {
      Date = date,
      Time = time,
      Direction = TrainDirection.JToW,
      Passenger = new PassengerRecord
      {
        FullName = "Test Passenger",
        Gender = PassengerGender.M,
        PassportExpiry = new DateOnly(2030, 1, 1),
        PassportNumber = "TEST123",
      },
    };

  [Fact]
  public void Departure_uses_singapore_wall_clock()
  {
    BookingPurchaseTiming
      .DepartureUtc(DepartureDate, DepartureTime)
      .Should()
      .Be(new DateTime(2026, 7, 15, 0, 30, 0, DateTimeKind.Utc));
  }

  [Fact]
  public void Exact_three_hour_boundary_is_purchasable()
  {
    var departureUtc = BookingPurchaseTiming.DepartureUtc(DepartureDate, DepartureTime);
    var now = new DateTimeOffset(departureUtc - TimeSpan.FromHours(3));

    BookingPurchaseTiming.CanPurchase(DepartureDate, DepartureTime, now).Should().BeTrue();
    BookingPurchaseTiming.Validate(Record(DepartureDate, DepartureTime), now).IsSuccess().Should().BeTrue();
  }

  [Fact]
  public void Anything_below_three_hours_is_rejected()
  {
    var departureUtc = BookingPurchaseTiming.DepartureUtc(DepartureDate, DepartureTime);
    var now = new DateTimeOffset(departureUtc - TimeSpan.FromHours(3) + TimeSpan.FromTicks(1));

    BookingPurchaseTiming.CanPurchase(DepartureDate, DepartureTime, now).Should().BeFalse();
    BookingPurchaseTiming
      .Validate(Record(DepartureDate, DepartureTime), now)
      .FailureOrDefault()
      .Should()
      .BeOfType<InvalidBookingOperationException>();
  }

  [Fact]
  public void Departed_slot_is_rejected_regardless_of_callers_offset()
  {
    // 08:30 SGT departed at 00:30 UTC. This instant is 10:00 SGT, expressed
    // as the previous calendar day in Los Angeles to guard the original bug.
    var losAngelesNow = new DateTimeOffset(2026, 7, 14, 19, 0, 0, TimeSpan.FromHours(-7));

    BookingPurchaseTiming
      .LeadTime(DepartureDate, DepartureTime, losAngelesNow)
      .Should()
      .Be(TimeSpan.FromMinutes(-90));
    BookingPurchaseTiming.CanPurchase(DepartureDate, DepartureTime, losAngelesNow).Should().BeFalse();
  }

  [Fact]
  public async Task Domain_create_rejects_cutoff_before_wallet_or_transaction_mutation()
  {
    var bookingRepo = RecordCalls<IBookingRepository>(out var bookingCalls);
    var storage = RecordCalls<IBookingStorage>(out var storageCalls);
    var wallet = RecordCalls<IWalletRepository>(out var walletCalls);
    var transactionRepo = RecordCalls<ITransactionRepository>(out var transactionRepoCalls);
    var transaction = RecordCalls<ITransactionManager>(out var transactionCalls);
    var transactionGenerator = RecordCalls<ITransactionGenerator>(out var generatorCalls);
    var fee = RecordCalls<IFeeCalculator>(out var feeCalls);
    var terminator = RecordCalls<IBookingTerminatorRepository>(out var terminatorCalls);
    var cdc = RecordCalls<IBookingCdcRepository>(out var cdcCalls);
    var notifications = RecordCalls<IBookingNotificationService>(out var notificationCalls);
    var settings = RecordCalls<IPrioritySettingsRepository>(out var settingsCalls);
    var access = RecordCalls<IPriorityAccessRepository>(out var accessCalls);

    var service = new BookingService(
      bookingRepo,
      storage,
      wallet,
      transactionRepo,
      transaction,
      transactionGenerator,
      fee,
      terminator,
      cdc,
      notifications,
      settings,
      access,
      NullLogger<BookingService>.Instance
    );
    var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

    var result = await service.Create("user", 10m, Record(yesterday, new TimeOnly(23, 59)));

    result.FailureOrDefault().Should().BeOfType<InvalidBookingOperationException>();
    walletCalls.Should().BeEmpty("the cutoff must run before wallet lookup or reservation");
    transactionCalls.Should().BeEmpty("the cutoff must run before opening a transaction");
    transactionRepoCalls.Should().BeEmpty();
    bookingCalls.Should().BeEmpty();
    storageCalls.Should().BeEmpty();
    generatorCalls.Should().BeEmpty();
    feeCalls.Should().BeEmpty();
    terminatorCalls.Should().BeEmpty();
    cdcCalls.Should().BeEmpty();
    notificationCalls.Should().BeEmpty();
    settingsCalls.Should().BeEmpty();
    accessCalls.Should().BeEmpty();
  }

  private static T RecordCalls<T>(out List<string> calls)
    where T : class
  {
    var service = DispatchProxy.Create<T, RecordingProxy>();
    var recorder = (RecordingProxy)(object)service;
    calls = recorder.Calls;
    return service;
  }

  public class RecordingProxy : DispatchProxy
  {
    public List<string> Calls { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      var method = targetMethod?.Name ?? "unknown";
      this.Calls.Add(method);
      throw new InvalidOperationException($"Unexpected call to {method}");
    }
  }
}
