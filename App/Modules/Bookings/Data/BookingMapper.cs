using App.Modules.Timings.Data;
using App.Modules.Transactions.Data;
using App.Modules.Users.Data;
using App.Modules.Wallets.Data;
using Domain.Booking;
using Domain.Passenger;

namespace App.Modules.Bookings.Data;

public static class BookingMapper
{
  // to Domain
  public static PassengerRecord ToRecord(this BookingPassengerData data) =>
    new()
    {
      Gender = (PassengerGender)data.Gender,
      FullName = data.FullName,
      PassportExpiry = data.PassportExpiry,
      PassportNumber = data.PassportNumber,
    };

  public static BookingRecord ToRecord(this BookingData data) =>
    new()
    {
      Date = data.Date,
      Time = data.Time,
      Direction = data.Direction.ToTrainDirection(),
      Passenger = data.Passenger.ToRecord(),
    };

  public static BookingStatus ToStatus(this BookingData data) =>
    new()
    {
      Status = (BookStatus)data.Status,
      CompletedAt = data.CompletedAt,
      LastBuyingAt = data.LastBuyingAt,
    };

  public static BookingComplete ToComplete(this BookingData data) =>
    new()
    {
      Ticket = data.Ticket,
      BookingNumber = data.BookingNo,
      TicketNumber = data.TicketNo,
      KtmbAmount = data.KtmbAmount,
      KtmbCurrency = data.KtmbCurrency,
    };

  public static BookingPrincipal ToPrincipal(this BookingData data) =>
    new()
    {
      Id = data.Id,
      UserId = data.UserId,
      CreatedAt = data.CreatedAt,
      Record = data.ToRecord(),
      Status = data.ToStatus(),
      Complete = data.ToComplete(),
      Priority = data.Priority,
      PriorityFee = data.PriorityFee,
      RecoveryRetries = data.RecoveryRetries,
    };

  public static Booking ToDomain(this BookingData data) =>
    new()
    {
      User = data.User.ToPrincipal(),
      Principal = data.ToPrincipal(),
      Transaction = data.Transaction.ToPrincipal(),
      Wallet = data.Transaction.Wallet.ToPrincipal(),
    };

  // price breakdown captured at purchase (persisted as owned JSON)
  public static BookingPriceBreakdownData ToData(this BookingPriceBreakdown b) =>
    new()
    {
      BaseCost = b.BaseCost,
      Lines = b
        .Lines.Select(l => new BookingPriceLineData
        {
          Kind = l.Kind,
          Name = l.Name,
          Delta = l.Delta,
        })
        .ToList(),
      Final = b.Final,
    };

  public static BookingPriceBreakdown ToBreakdown(this BookingPriceBreakdownData d) =>
    new()
    {
      BaseCost = d.BaseCost,
      Lines = d
        .Lines.Select(l => new BookingPriceLine
        {
          Kind = l.Kind,
          Name = l.Name,
          Delta = l.Delta,
        })
        .ToArray(),
      Final = d.Final,
    };

  // To Data
  public static BookingPassengerData ToData(this PassengerRecord p) =>
    new()
    {
      Gender = (byte)p.Gender,
      FullName = p.FullName,
      PassportExpiry = p.PassportExpiry,
      PassportNumber = p.PassportNumber,
    };

  public static BookingData UpdateData(this BookingData data, BookingRecord record)
  {
    data.Date = record.Date;
    data.Time = record.Time;
    data.Direction = record.Direction.ToData();
    data.Passenger = record.Passenger.ToData();
    return data;
  }

  public static BookingData UpdateData(this BookingData data, BookingComplete complete)
  {
    data.Ticket = complete.Ticket;
    data.BookingNo = complete.BookingNumber;
    data.TicketNo = complete.TicketNumber;
    data.KtmbAmount = complete.KtmbAmount;
    data.KtmbCurrency = complete.KtmbCurrency;
    return data;
  }

  public static BookingData UpdateData(this BookingData data, BookingStatus status)
  {
    // stamp the Buying entry time at the single status-write choke point;
    // callers cannot set it and other transitions preserve the last stamp
    if (status.Status == BookStatus.Buying && (BookStatus)data.Status != BookStatus.Buying)
      data.LastBuyingAt = DateTime.UtcNow;
    data.Status = (byte)status.Status;
    data.CompletedAt = status.CompletedAt;
    return data;
  }
}
