using Domain.Booking;

namespace Domain.Transaction;

public interface ITransactionGenerator
{
  public TransactionRecord CreateBooking(decimal cost, BookingRecord booking);

  public TransactionRecord CompleteBooking(TransactionRecord create, BookingRecord booking);

  public TransactionRecord RefundBooking(TransactionRecord create, BookingRecord booking);

  public TransactionRecord CancelBooking(TransactionRecord create, BookingRecord booking);


  public TransactionRecord TerminateBooking(TransactionRecord create, BookingRecord booking);
}

public class TransactionGenerator(IRefundCalculator calculator) : ITransactionGenerator
{
  public TransactionRecord CreateBooking(decimal cost, BookingRecord booking)
  {
    return new TransactionRecord
    {
      Name = "Purchased Booking Service",
      Description = $"Purchased ticket booking service for SGD {cost} for '{booking.Passenger.FullName}'. The" +
                    $"KTMB ticket is in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} at {booking.Time.ToHuman()}. The " +
                    $"amount, SGD {cost} will be placed in reserve until the booking is completed or cancelled.",
      Type = TransactionType.BookingRequest,
      Amount = cost,
      From = Accounts.Usable.DisplayName,
      To = Accounts.BookingReserve.DisplayName,
    };
  }

  public TransactionRecord CompleteBooking(TransactionRecord create, BookingRecord booking)
  {
    return new TransactionRecord
    {
      Name = "Ticket Booking Successful",
      Description = $"Successfully purchased" +
                    $"KTMB ticket in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} at " +
                    $"{booking.Time.ToHuman()}. " +
                    $"SGD {create.Amount} that was placed in the wallet reserve has been deducted.",
      Type = TransactionType.BookingComplete,
      Amount = create.Amount,
      From = Accounts.BookingReserve.DisplayName,
      To = Accounts.BunnyBooker.DisplayName,
    };
  }

  public TransactionRecord RefundBooking(TransactionRecord create, BookingRecord booking)
  {
    return new TransactionRecord
    {
      Name = "Ticket Booking Refunded",
      Description =
        $"The Booking for KTMB ticket in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} " +
        $"at {booking.Time.ToHuman()} has failed. " +
        $"SGD {create.Amount} that was placed in reserve has been fully refunded to your wallet.",
      Type = TransactionType.BookingRefund,
      Amount = create.Amount,
      From = Accounts.BookingReserve.DisplayName,
      To = Accounts.Usable.DisplayName,
    };
  }

  public TransactionRecord CancelBooking(TransactionRecord create, BookingRecord booking)
  {
    return new TransactionRecord
    {
      Name = "Ticket Booking Cancelled",
      Description =
        $"KTMB ticket in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} at {booking.Time.ToHuman()} been " +
        $"has been cancelled by you." +
        $"SGD {create.Amount} that was placed in reserve, SGD {create.Amount} has been refunded to your wallet.",
      Type = TransactionType.BookingCancel,
      Amount = create.Amount,
      From = Accounts.BookingReserve.DisplayName,
      To = Accounts.Usable.DisplayName,
    };
  }

  public TransactionRecord TerminateBooking(TransactionRecord create, BookingRecord booking)
  {
    var refund = create.Amount * calculator.RefundRate;
    var penalty = create.Amount * calculator.PenaltyRate;
    return new TransactionRecord
    {
      Name = "Ticket Booking Terminated",
      Description =
        $"KTMB ticket in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} " +
        $"at {booking.Time.ToHuman()} been has been terminated by you after BunnyBooker has " +
        $"secured your KTMB ticket on KITS. SGD {refund} has been refunded to your wallet from" +
        $" BunnyBooker while the remaining SGD {penalty} will be kept by BunnyBooker.",
      Type = TransactionType.BookingTerminated,
      Amount = refund,
      From = Accounts.BunnyBooker.DisplayName,
      To = Accounts.Usable.DisplayName,
    };
  }
}
