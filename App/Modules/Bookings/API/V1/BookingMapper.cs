using App.Modules.Passengers.API.V1;
using App.Modules.Timings.API.V1;
using App.Modules.Users.API.V1;
using App.Utility;
using Domain.Booking;
using Domain.Passenger;

namespace App.Modules.Bookings.API.V1;

public static class BookingMapper
{
  // RES

  public static string ToRes(this BookStatus status) =>
    status switch
    {
      BookStatus.Pending => "Pending",
      BookStatus.Buying => "Buying",
      BookStatus.Completed => "Completed",
      BookStatus.Cancelled => "Cancelled",
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static BookingPassengerRes ToRes(this PassengerRecord p) =>
    new(p.FullName, p.Gender.ToRes(), p.PassportExpiry.ToStandardDateFormat(), p.PassportNumber);

  public static BookingPrincipalRes ToRes(this BookingPrincipal p)
  {
    return new BookingPrincipalRes(
      p.Id,
      p.Record.Date.ToStandardDateFormat(),
      p.Record.Time.ToStandardTimeFormat(),
      p.Record.Direction.ToRes(),
      p.Record.Passengers.Select(x => x.ToRes()),
      p.CreatedAt,
      p.Status.CompletedAt,
      p.Complete.Ticket,
      p.Status.Status.ToRes()
    );
  }



  public static BookingRes ToRes(this Booking p)
    => new(p.Principal.ToRes(), p.User.ToRes());


  public static BookingCountRes ToRes(this BookingCount p) =>
    new(p.Date.ToStandardDateFormat(), p.Time.ToStandardTimeFormat(), p.Direction.ToRes(), p.TicketsNeeded);

  // REQ
  public static PassengerRecord ToRecord(this BookingPassengerReq req) =>
    new()
    {
      Gender = req.Gender.GenderToDomain(),
      FullName = req.FullName,
      PassportExpiry = req.PassportExpiry.ToDate(),
      PassportNumber = req.PassportNumber,
    };

  public static BookingRecord ToRecord(this CreateBookingReq req) =>
    new()
    {
      Date = req.Date.ToDate(),
      Time = req.Time.ToTime(),
      Direction = req.Direction.DirectionToDomain(),
      Passengers = req.Passengers.Select(r => r.ToRecord()),
    };

  public static BookingRecord ToRecord(this UpdateBookingReq req) =>
    new()
    {
      Date = req.Date.ToDate(),
      Time = req.Time.ToTime(),
      Direction = req.Direction.DirectionToDomain(),
      Passengers = req.Passengers.Select(r => r.ToRecord()),
    };

  public static BookingSearch ToDomain(this SearchBookingQuery query) =>
    new()
    {
      Date = query.Date?.ToDate(),
      Time = query.Time?.ToTime(),
      Direction = query.Direction?.DirectionToDomain(),
      UserId = query.UserId,
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };
}
