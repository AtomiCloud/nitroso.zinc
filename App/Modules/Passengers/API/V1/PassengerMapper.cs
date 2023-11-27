using App.Modules.Passengers.API.V1;
using App.Modules.Users.API.V1;
using App.Utility;
using Domain.Passenger;

namespace App.Modules.Passengers.API.V1;

public static class PassengerMapper
{
  // RES
  public static PassengerPrincipalRes ToRes(this PassengerPrincipal p)
    => new(p.Id,
      p.Record.FullName,
      p.Record.Gender.ToRes(),
      p.Record.PassportExpiry.ToStandardDateFormat(),
      p.Record.PassportNumber);

  public static PassengerRes ToRes(this Passenger p)
    => new(p.Principal.ToRes(), p.User.ToRes());

  public static string ToRes(this PassengerGender gender) =>
    gender switch
    {
      PassengerGender.M => "M",
      PassengerGender.F => "F",
      _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, "Failed to convert gender to response model")
    };


  // REQ
  public static PassengerGender GenderToDomain(this string gender) =>
    gender switch
    {
      "M" => PassengerGender.M,
      "F" => PassengerGender.F,
      _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, "Failed to convert gender from request model")
    };


  public static PassengerRecord ToRecord(this CreatePassengerReq req) =>
    new()
    {
      Gender = req.Gender.GenderToDomain(),
      FullName = req.FullName,
      PassportExpiry = req.PassportExpiry.ToDate(),
      PassportNumber = req.PassportNumber,
    };

  public static PassengerRecord ToRecord(this UpdatePassengerReq req) =>
    new()
    {
      Gender = req.Gender.GenderToDomain(),
      FullName = req.FullName,
      PassportExpiry = req.PassportExpiry.ToDate(),
      PassportNumber = req.PassportNumber,
    };

  public static PassengerSearch ToDomain(this SearchPassengerQuery query) =>
    new()
    {
      UserId = query.UserId,
      Name = query.Name,
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };
}
