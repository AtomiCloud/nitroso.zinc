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
  public static PassengerRecord ToRecord(this BookingPassengerData data) => new()
  {
    Gender = (PassengerGender)data.Gender,
    FullName = data.FullName,
    PassportExpiry = data.PassportExpiry,
    PassportNumber = data.PassportNumber,
  };

  public static BookingRecord ToRecord(this BookingData data) => new()
  {
    Date = data.Date,
    Time = data.Time,
    Direction = data.Direction.ToTrainDirection(),
    Passenger = data.Passenger.ToRecord(),
  };

  public static BookingStatus ToStatus(this BookingData data) => new()
  {
    Status = (BookStatus)data.Status,
    CompletedAt = data.CompletedAt,
  };

  public static BookingComplete ToComplete(this BookingData data) => new() { Ticket = data.Ticket, };

  public static BookingPrincipal ToPrincipal(this BookingData data) => new()
  {
    Id = data.Id,
    UserId = data.UserId,
    CreatedAt = data.CreatedAt,
    Record = data.ToRecord(),
    Status = data.ToStatus(),
    Complete = data.ToComplete(),
  };


  public static Booking ToDomain(this BookingData data) => new()
  {
    User = data.User.ToPrincipal(),
    Principal = data.ToPrincipal(),
    Transaction = data.Transaction.ToPrincipal(),
    Wallet = data.Transaction.Wallet.ToPrincipal(),
  };

  // To Data
  public static BookingPassengerData ToData(this PassengerRecord p) => new()
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
    return data;
  }

  public static BookingData UpdateData(this BookingData data, BookingStatus status)
  {
    data.Status = (byte)status.Status;
    data.CompletedAt = status.CompletedAt;
    return data;
  }
}
