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
    var generator = new TransactionGenerator();

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
    var generator = new TransactionGenerator();

    var record = generator.PriorityFeeCharge(10m, SampleBooking());

    record.Type.Should().Be(TransactionType.PriorityFee);
    record.Amount.Should().Be(10m);
    record.From.Should().Be(Accounts.Usable.DisplayName);
    record.To.Should().Be(Accounts.PriorityFee.DisplayName);
  }

  [Fact]
  public void TerminateBooking_moves_the_refund_and_renders_the_actual_numbers()
  {
    var generator = new TransactionGenerator();

    // fee-engine numbers: SGD 16.05 amount, SGD 6.42 fee → SGD 9.63 refund
    var record = generator.TerminateBooking(SampleBooking(), 9.63m, 6.42m);

    record.Type.Should().Be(TransactionType.BookingTerminated);
    record.Amount.Should().Be(9.63m, "only the refund moves back to the wallet");
    record.From.Should().Be(Accounts.BunnyBooker.DisplayName);
    record.To.Should().Be(Accounts.Usable.DisplayName);
    record.Description.Should().Contain("SGD 9.63", "the description shows the actual refund");
    record.Description.Should().Contain("SGD 6.42", "the description shows the actual fee kept");
  }

  [Fact]
  public void TerminateBooking_renders_a_zero_fee_termination_as_a_full_refund()
  {
    var generator = new TransactionGenerator();

    var record = generator.TerminateBooking(SampleBooking(), 16.05m, 0m);

    record.Amount.Should().Be(16.05m);
    record.Description.Should().Contain("SGD 16.05");
    record.Description.Should().Contain("SGD 0.00");
  }

  [Fact]
  public void RefundPriorityFee_reverses_the_charge()
  {
    var generator = new TransactionGenerator();

    var record = generator.RefundPriorityFee(10m, SampleBooking());

    record.Type.Should().Be(TransactionType.PriorityFee);
    record.Amount.Should().Be(10m);
    record.From.Should().Be(Accounts.PriorityFee.DisplayName);
    record.To.Should().Be(Accounts.Usable.DisplayName);
  }
}
