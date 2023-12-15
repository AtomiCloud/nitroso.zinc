using App.Modules.Users.API.V1;

namespace App.Modules.Bookings.API.V1;

public record SearchBookingQuery(string? Date, string? Direction, string? Time, string? UserId, int? Limit, int? Skip);

// REQ
public record BookingPassengerReq(string FullName, string Gender, string PassportExpiry, string PassportNumber);

public record CreateBookingReq(string Date, string Time, string Direction, IEnumerable<BookingPassengerReq> Passengers);

public record UpdateBookingReq(string Date, string Time, string Direction, IEnumerable<BookingPassengerReq> Passengers);

// RESP
public record BookingPassengerRes(string FullName, string Gender, string PassportExpiry, string PassportNumber);

public record BookingPrincipalRes(
  Guid Id,
  string Date,
  string Time,
  string Direction,
  IEnumerable<BookingPassengerRes> Passengers,
  DateTime CreatedAt,
  DateTime? CompletedAt,
  string Status
);

public record BookingRes(BookingPrincipalRes Principal, UserPrincipalRes User);

public record BookingCountRes(string Date, string Time, string Direction, int TicketsNeeded);