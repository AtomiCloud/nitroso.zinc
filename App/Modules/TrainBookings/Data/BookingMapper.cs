using App.Modules.Users.Data;
using Domain.Booking;
using Domain.Passenger;

namespace App.Modules.TrainBookings.Data;

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
    Passengers = data.Passengers.Select(x => x.ToRecord()),
  };

  public static BookingStatus ToStatus(this BookingData data) => new()
  {
    Status = (BookStatus)data.Status,
    CompletedAt = data.CompletedAt,
  };

  public static BookingPrincipal ToPrincipal(this BookingData data) => new()
  {
    Id = data.Id,
    UserId = data.UserId,
    CreatedAt = data.CreatedAt,
    Record = data.ToRecord(),
    Status = data.ToStatus(),
  };


  public static Booking ToDomain(this BookingData data) => new()
  {
    User = data.User.ToPrincipal(),
    Principal = data.ToPrincipal(),
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
    data.Passengers = record.Passengers
      .Select(passenger => passenger.ToData())
      .ToArray();

    return data;
  }

  public static BookingData UpdateData(this BookingData data, BookingStatus status)
  {
    data.Status = (byte)status.Status;
    data.CompletedAt = status.CompletedAt;
    return data;
  }
}
