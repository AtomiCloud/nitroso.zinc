using App.Modules.Users.Data;
using Domain.Passenger;

namespace App.Modules.Passengers.Data;

public static class PassengerMapper
{
  // to Domain
  public static PassengerRecord ToRecord(this PassengerData data) => new()
  {
    Gender = (PassengerGender)data.Gender,
    FullName = data.FullName,
    PassportExpiry = data.PassportExpiry,
    PassportNumber = data.PassportNumber,
  };

  public static PassengerPrincipal ToPrincipal(this PassengerData data) => new()
  {
    Id = data.Id,
    Record = data.ToRecord(),
    UserId = data.UserId,
  };


  public static Passenger ToDomain(this PassengerData data) => new()
  {
    User = data.User.ToPrincipal(),
    Principal = data.ToPrincipal(),
  };

  // To Data
  public static PassengerData EnrichData(this PassengerData data, PassengerRecord record)
  {
    data.Gender = (int)record.Gender;
    data.FullName = record.FullName;
    data.PassportExpiry = record.PassportExpiry;
    data.PassportNumber = record.PassportNumber;
    return data;
  }
}
