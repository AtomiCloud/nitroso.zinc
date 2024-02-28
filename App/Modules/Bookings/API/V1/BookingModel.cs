using App.Modules.Users.API.V1;

namespace App.Modules.Bookings.API.V1;

public record SearchBookingQuery(
  string? Date,
  string? Direction,
  string? Status,
  string? Time,
  string? UserId,
  int? Limit,
  int? Skip);

public record ReserveBookingQuery(string Date, string Direction, string Time);

public record BookingCountQuery(string Date, string Direction);

// REQ
public record BookingPassengerReq(string FullName, string Gender, string PassportExpiry, string PassportNumber);

public record CreateBookingReq(string Date, string Time, string Direction, BookingPassengerReq Passenger);

public record UpdateBookingReq(string Date, string Time, string Direction, BookingPassengerReq Passenger);

// RESP
public record BookingPassengerRes(string FullName, string Gender, string PassportExpiry, string PassportNumber);

public record BookingPrincipalRes(
  Guid Id,
  string Date,
  string Time,
  string Direction,
  BookingPassengerRes Passenger,
  DateTime CreatedAt,
  DateTime? CompletedAt,
  string? TicketLink,
  string? TicketNo,
  string? BookingNo,
  string Status
);

public record BookingRes(BookingPrincipalRes Principal, UserPrincipalRes User);

public record BookingCountRes(string Date, string Time, string Direction, int TicketsNeeded);
