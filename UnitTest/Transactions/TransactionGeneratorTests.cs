using App.Modules.Transactions.API.V1;
using Domain;
using Domain.Booking;
using Domain.Passenger;
using Domain.Timings;
using Domain.Transaction;
using FluentAssertions;

namespace UnitTest.Transactions;

public class TransactionGeneratorTests
{
  private sealed class StubCalculator : IRefundCalculator
  {
    public decimal RefundRate => 0.7m;
    public decimal PenaltyRate => 0.3m;
  }

  private static BookingRecord SampleBooking() =>
    new()
    {
      Date = new DateOnly(2026, 7, 3),
      Time = new TimeOnly(13, 45),
      Direction = TrainDirection.WToJ,
      Passenger = new PassengerRecord
      {
        FullName = "TEST PASSENGER",
        Gender = PassengerGender.M,
        PassportExpiry = new DateOnly(2030, 1, 1),
        PassportNumber = "E1234567X",
      },
    };

  private static TransactionRecord SampleCreate() =>
    new()
    {
      Name = "Purchased Booking Service",
      Description = "test",
      Type = TransactionType.BookingRequest,
      Amount = 16.05m,
      From = Accounts.Usable.DisplayName,
      To = Accounts.BookingReserve.DisplayName,
    };

  [Fact]
  public void DuplicateBooking_refunds_full_amount_from_reserve_to_usable()
  {
    var generator = new TransactionGenerator(new StubCalculator());

    var record = generator.DuplicateBooking(SampleCreate(), SampleBooking());

    record.Type.Should().Be(TransactionType.BookingDuplicate);
    record.Amount.Should().Be(16.05m);
    record.From.Should().Be(Accounts.BookingReserve.DisplayName);
    record.To.Should().Be(Accounts.Usable.DisplayName);
  }

  [Fact]
  public void TransactionType_ToRes_covers_all_enum_values()
  {
    foreach (var type in Enum.GetValues<TransactionType>())
    {
      var act = () => type.ToRes();
      act.Should().NotThrow($"TransactionType.{type} must have a ToRes mapping");
    }
  }

  [Fact]
  public void TransactionType_round_trips_every_enum_value()
  {
    foreach (var type in Enum.GetValues<TransactionType>())
    {
      type.ToRes().ToTransactionType().Should().Be(type);
    }
  }

  [Fact]
  public void TransactionTypes_values_contains_booking_duplicate()
  {
    TransactionTypes.Values.Should().Contain(TransactionTypes.BookingDuplicate);
    ((int)TransactionType.BookingDuplicate).Should().Be(12);
  }

  [Fact]
  public void TransactionTypes_values_contains_priority_fee()
  {
    TransactionTypes.Values.Should().Contain(TransactionTypes.PriorityFee);
    ((int)TransactionType.PriorityFee).Should().Be(15);
  }

  [Fact]
  public void PriorityFeeCharge_moves_the_fee_from_usable_to_the_priority_fee_account()
  {
    var generator = new TransactionGenerator(new StubCalculator());

    var record = generator.PriorityFeeCharge(10m, SampleBooking());

    record.Type.Should().Be(TransactionType.PriorityFee);
    record.Amount.Should().Be(10m);
    record.From.Should().Be(Accounts.Usable.DisplayName);
    record.To.Should().Be(Accounts.PriorityFee.DisplayName);
  }

  [Fact]
  public void RefundPriorityFee_reverses_the_charge()
  {
    var generator = new TransactionGenerator(new StubCalculator());

    var record = generator.RefundPriorityFee(10m, SampleBooking());

    record.Type.Should().Be(TransactionType.PriorityFee);
    record.Amount.Should().Be(10m);
    record.From.Should().Be(Accounts.PriorityFee.DisplayName);
    record.To.Should().Be(Accounts.Usable.DisplayName);
  }
}
