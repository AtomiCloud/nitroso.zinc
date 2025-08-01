using Domain.Booking;
using Domain.Payment;
using Domain.Withdrawal;

namespace Domain.Transaction;

public interface ITransactionGenerator
{
  public TransactionRecord CreateBooking(decimal cost, BookingRecord booking);

  public TransactionRecord CompleteBooking(TransactionRecord create, BookingRecord booking);

  public TransactionRecord RefundBooking(TransactionRecord create, BookingRecord booking);

  public TransactionRecord CancelBooking(TransactionRecord create, BookingRecord booking);

  public TransactionRecord TerminateBooking(TransactionRecord create, BookingRecord booking);

  // Admin Flow
  public TransactionRecord AdminInflow(decimal amount, string description);

  public TransactionRecord AdminOutflow(decimal amount, string description);

  public TransactionRecord Promotional(decimal amount, string description);

  // Withdrawal
  public TransactionRecord CreateWithdrawalRequest(WithdrawalRecord record);

  public TransactionRecord CompleteWithdrawalRequest(WithdrawalRecord record);

  public TransactionRecord CancelWithdrawalRequest(WithdrawalRecord record);

  public TransactionRecord RejectWithdrawalRequest(WithdrawalRecord record);

  public TransactionRecord Deposit(PaymentPrincipal principal);
}

public class TransactionGenerator(IRefundCalculator calculator) : ITransactionGenerator
{
  public TransactionRecord CreateBooking(decimal cost, BookingRecord booking)
  {
    return new TransactionRecord
    {
      Name = "Purchased Booking Service",
      Description =
        $"Purchased ticket booking service for SGD {cost:0.00} for '{booking.Passenger.FullName}'. The"
        + $"KTMB ticket is in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} at {booking.Time.ToHuman()}. The "
        + $"amount, SGD {cost:0.00} will be placed in reserve until the booking is completed or cancelled.",
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
      Description =
        $"Successfully purchased"
        + $"KTMB ticket in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} at "
        + $"{booking.Time.ToHuman()}. "
        + $"SGD {create.Amount:0.00} that was placed in the wallet reserve has been deducted.",
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
        $"The Booking for KTMB ticket in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} "
        + $"at {booking.Time.ToHuman()} has failed. "
        + $"SGD {create.Amount:0.00} that was placed in reserve has been fully refunded to your wallet.",
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
        $"KTMB ticket in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} at {booking.Time.ToHuman()} been "
        + $"has been cancelled by you."
        + $"SGD {create.Amount:0.00} that was placed in reserve, SGD {create.Amount:0.00} has been refunded to your wallet.",
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
        $"KTMB ticket in the direction '{booking.Direction.ToHuman()}' on {booking.Date.ToHuman()} "
        + $"at {booking.Time.ToHuman()} been has been terminated by you after BunnyBooker has "
        + $"secured your KTMB ticket on KITS. SGD {refund:0.00} has been refunded to your wallet from"
        + $" BunnyBooker while the remaining SGD {penalty:0.00} will be kept by BunnyBooker.",
      Type = TransactionType.BookingTerminated,
      Amount = refund,
      From = Accounts.BunnyBooker.DisplayName,
      To = Accounts.Usable.DisplayName,
    };
  }

  public TransactionRecord AdminInflow(decimal amount, string description)
  {
    return new TransactionRecord
    {
      Name = "BunnyBooker Admin Inflow",
      Description =
        $"The BunnyBooker Admin has transferred SGD {amount:0.00} credits to your Usable account. "
        + description,
      Type = TransactionType.Transfer,
      Amount = amount,
      From = Accounts.BunnyBooker.DisplayName,
      To = Accounts.Usable.DisplayName,
    };
  }

  public TransactionRecord AdminOutflow(decimal amount, string description)
  {
    return new TransactionRecord
    {
      Name = "BunnyBooker Admin Outflow",
      Description =
        $"The BunnyBooker Admin has transferred SGD ${amount:0.00} credits out of your Usable account. "
        + description,
      Type = TransactionType.Transfer,
      Amount = amount,
      From = Accounts.Usable.DisplayName,
      To = Accounts.BunnyBooker.DisplayName,
    };
  }

  public TransactionRecord Promotional(decimal amount, string description)
  {
    return new TransactionRecord
    {
      Name = "Promotional Credits",
      Description = description,
      Amount = amount,
      Type = TransactionType.Promotional,
      From = Accounts.BunnyBooker.DisplayName,
      To = Accounts.Usable.DisplayName,
    };
  }

  public TransactionRecord CreateWithdrawalRequest(WithdrawalRecord record)
  {
    var amount = record.Amount;
    return new TransactionRecord
    {
      Name = "Withdrawal Request",
      Description =
        $"A withdrawal request of SGD {amount:0.00} has been made to the PayNow "
        + $"account {record.PayNowNumber}. SGD {amount:0.00} has been moved from your Usable account "
        + $" to your Withdrawal Reserve account.",
      Amount = amount,
      Type = TransactionType.WithdrawRequest,
      From = Accounts.Usable.DisplayName,
      To = Accounts.WithdrawReserve.DisplayName,
    };
  }

  public TransactionRecord CompleteWithdrawalRequest(WithdrawalRecord record)
  {
    var amount = record.Amount;
    return new TransactionRecord
    {
      Name = "Withdrawal Completed",
      Description =
        $"BunnyBooker has completed your withdrawal request of SGD {amount:0.00} to the PayNow "
        + $"account {record.PayNowNumber}. SGD {amount:0.00} has been collected from your "
        + $"Withdrawal Reserve Account.",
      Amount = amount,
      Type = TransactionType.WithdrawComplete,
      From = Accounts.WithdrawReserve.DisplayName,
      To = Accounts.BunnyBooker.DisplayName,
    };
  }

  public TransactionRecord CancelWithdrawalRequest(WithdrawalRecord record)
  {
    var amount = record.Amount;
    return new TransactionRecord
    {
      Name = "Withdrawal Cancelled",
      Description =
        $"The Withdrawal Request of SGD {amount:0.00} to the PayNow "
        + $"account {record.PayNowNumber} has been cancelled. SGD {amount:0.00} has been moved to your "
        + $"Usable Account from your Withdraw Reserve Account.",
      Amount = amount,
      Type = TransactionType.WithdrawCancelled,
      From = Accounts.WithdrawReserve.DisplayName,
      To = Accounts.Usable.DisplayName,
    };
  }

  public TransactionRecord RejectWithdrawalRequest(WithdrawalRecord record)
  {
    var amount = record.Amount;
    return new TransactionRecord
    {
      Name = "Withdrawal Rejected",
      Description =
        $"The Withdrawal Request of SGD {amount:0.00} to the PayNow "
        + $"account {record.PayNowNumber} has been rejected. "
        + $"SGD {amount:0.00} has been moved to your "
        + $"Usable Account from your Withdraw Reserve Account.",
      Amount = amount,
      Type = TransactionType.WithdrawRejected,
      From = Accounts.WithdrawReserve.DisplayName,
      To = Accounts.Usable.DisplayName,
    };
  }

  public TransactionRecord Deposit(PaymentPrincipal principal)
  {
    var amount = principal.Record.CapturedAmount;
    return new TransactionRecord
    {
      Name = $"Deposit via {principal.Reference.Gateway}",
      Description = $"The deposit of SGD {amount:0.00} to your Usable Account",
      Amount = amount,
      Type = TransactionType.Deposit,
      From = Accounts.BunnyBooker.DisplayName,
      To = Accounts.Usable.DisplayName,
    };
  }
}
