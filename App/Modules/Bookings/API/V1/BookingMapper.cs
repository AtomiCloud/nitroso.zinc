using App.Modules.Passengers.API.V1;
using App.Modules.Timings.API.V1;
using App.Modules.Users.API.V1;
using App.Utility;
using Domain.Booking;
using Domain.Passenger;

namespace App.Modules.Bookings.API.V1;

public static class BookingMapper
{
  // DOMAIN -> RES

  public static string ToRes(this BookStatus status) =>
    status switch
    {
      BookStatus.Pending => "Pending",
      BookStatus.Buying => "Buying",
      BookStatus.Completed => "Completed",
      BookStatus.Cancelled => "Cancelled",
      BookStatus.Refunded => "Refunded",
      BookStatus.Terminated => "Terminated",
      BookStatus.Recovering => "Recovering",
      BookStatus.Duplicate => "Duplicate",
      BookStatus.RequireManualIntervention => "RequireManualIntervention",
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static BookingPassengerRes ToRes(this PassengerRecord p) =>
    new(p.FullName, p.Gender.ToRes(), p.PassportExpiry.ToStandardDateFormat(), p.PassportNumber);

  public static BookingPrincipalRes ToRes(this BookingPrincipal p)
  {
    return new BookingPrincipalRes(
      p.Id,
      p.UserId,
      p.Record.Date.ToStandardDateFormat(),
      p.Record.Time.ToStandardTimeFormat(),
      p.Record.Direction.ToRes(),
      p.Record.Passenger.ToRes(),
      p.CreatedAt,
      p.Status.CompletedAt,
      p.Complete.Ticket,
      p.Complete.TicketNumber,
      p.Complete.BookingNumber,
      p.Status.Status.ToRes()
    );
  }

  public static BookingRes ToRes(this Booking p) => new(p.Principal.ToRes(), p.User.ToRes());

  public static BookingCountRes ToRes(this BookingCount p) =>
    new(
      p.Date.ToStandardDateFormat(),
      p.Time.ToStandardTimeFormat(),
      p.Direction.ToRes(),
      p.TicketsNeeded
    );

  // REQ -> DOMAIN
  public static BookingCountSearch ToDomain(this BookingCountQuery q) =>
    new() { Date = q.Date.ToDate(), Direction = q.Direction.DirectionToDomain() };

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
      Passenger = req.Passenger.ToRecord(),
    };

  public static BookingRecord ToRecord(this UpdateBookingReq req) =>
    new()
    {
      Date = req.Date.ToDate(),
      Time = req.Time.ToTime(),
      Direction = req.Direction.DirectionToDomain(),
      Passenger = req.Passenger.ToRecord(),
    };

  public static BookStatus ToBookStatus(this string status) =>
    status switch
    {
      "Pending" => BookStatus.Pending,
      "Buying" => BookStatus.Buying,
      "Completed" => BookStatus.Completed,
      "Cancelled" => BookStatus.Cancelled,
      "Refunded" => BookStatus.Refunded,
      "Terminated" => BookStatus.Terminated,
      "Recovering" => BookStatus.Recovering,
      "Duplicate" => BookStatus.Duplicate,
      "RequireManualIntervention" => BookStatus.RequireManualIntervention,
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static BookingSearch ToDomain(this SearchBookingQuery query) =>
    new()
    {
      Date = query.Date?.ToDate(),
      Time = query.Time?.ToTime(),
      Status = query.Status?.ToBookStatus(),
      Direction = query.Direction?.DirectionToDomain(),
      UserId = query.UserId,
      PassportNumber = query.PassportNumber,
      // resolved to an absolute cutoff server-side so callers (e.g. the
      // reverter cron) need no clock agreement with zinc
      BuyingBefore =
        query.StuckForMinutes != null
          ? DateTime.UtcNow.AddMinutes(-query.StuckForMinutes.Value)
          : null,
      Sort = query.SortBy?.ToBookingSort(),
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };

  public static BookingSort ToBookingSort(this string sort) =>
    sort switch
    {
      "Timing" => BookingSort.Timing,
      "PassengerName" => BookingSort.PassengerName,
      "PassportNumber" => BookingSort.PassportNumber,
      "BuyTime" => BookingSort.BuyTime,
      "FulfilTime" => BookingSort.FulfilTime,
      _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null),
    };
}
