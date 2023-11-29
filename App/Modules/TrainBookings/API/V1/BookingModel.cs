using App.Modules.Users.API.V1;

namespace App.Modules.TrainBookings.API.V1;

public record SearchBookingQuery(string? Date, string? Time, string? UserId, int? Limit, int? Skip);

// REQ
public record BookingPassengerReq(string FullName, string Gender, string PassportExpiry, string PassportNumber);

public record CreateBookingReq(string Date, string Time, IEnumerable<BookingPassengerReq> Passengers);

public record UpdateBookingReq(string Date, string Time, IEnumerable<BookingPassengerReq> Passengers);

// RESP
public record BookingPassengerRes(string FullName, string Gender, string PassportExpiry, string PassportNumber);

public record BookingPrincipalRes(
  Guid Id,
  string Date,
  string Time,
  IEnumerable<BookingPassengerRes> Passengers,
  DateTime CreatedAt,
  DateTime? CompletedAt,
  string Status
);

public record BookingRes(BookingPrincipalRes Principal, UserPrincipalRes User);

public record BookingCountRes(string Date, string Time, int TicketsNeeded);
