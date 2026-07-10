using App.Modules.Users.API.V1;

namespace App.Modules.Bookings.API.V1;

public record SearchBookingQuery(
  string? Date,
  string? Direction,
  string? Status,
  string? Time,
  string? UserId,
  string? PassportNumber,
  string? PassengerName,
  int? StuckForMinutes,
  // true = only Completed bookings without a ticket file reference — the
  // ticket-repair worklist
  bool? MissingTicket,
  string? SortBy,
  int? Limit,
  int? Skip
);

public record BookingStatsQueryReq(string? After, string? Before);

public record ReserveBookingQuery(string Date, string Direction, string Time);

public record BookingCountQuery(string Date, string Direction);

// REQ
public record BookingPassengerReq(
  string FullName,
  string Gender,
  string PassportExpiry,
  string PassportNumber
);

public record CreateBookingReq(
  string Date,
  string Time,
  string Direction,
  BookingPassengerReq Passenger,
  // Canonical quote returned by Cost/summary. Purchase rejects atomically if
  // pricing changed after the customer opened confirmation. Optional for one
  // release: old (raichu) argon clients don't send the quote yet — tighten to
  // required after raichu argon ships it.
  string? ExpectedCost
);

public record UpdateBookingReq(
  string Date,
  string Time,
  string Direction,
  BookingPassengerReq Passenger
);

// RESP
public record BookingPassengerRes(
  string FullName,
  string Gender,
  string PassportExpiry,
  string PassportNumber
);

public record BookingPrincipalRes(
  Guid Id,
  string UserId,
  string Date,
  string Time,
  string Direction,
  BookingPassengerRes Passenger,
  DateTime CreatedAt,
  DateTime? CompletedAt,
  string? TicketLink,
  string? TicketNo,
  string? BookingNo,
  string Status,
  bool Priority,
  // times this booking was recycled from Recovering back to Pending
  int RecoveryRetries
);

public record BookingRes(BookingPrincipalRes Principal, UserPrincipalRes User);

public record BookingCountRes(string Date, string Time, string Direction, int TicketsNeeded);

// total rows matching a search, ignoring Limit/Skip — for real page numbers
public record SearchCountRes(int Total);

// Position/Total null when the booking is no longer queued; Position is
// 1-based (1 = next to be bought)
public record BookingQueuePositionRes(string Status, int? Position, int? Total);

// ticket reference health: HasRef = the booking carries a ticket key,
// RefValid = that key resolves to a real object in block storage
public record BookingTicketHealthRes(bool HasRef, bool RefValid);

public record BookingStatRes(
  string DayOfWeek,
  string Time,
  string Direction,
  string Bucket,
  bool Priority,
  string DemandBucket,
  string? DeliveryBucket,
  int Total,
  int Completed,
  int Refunded,
  int Cancelled,
  int Terminated,
  int Other
);

// Priority queue

// REQ: replace the priority settings (insert-only latest, like Cost).
// Window times are SGT HH:mm:ss; both null = always available; start > end
// wraps midnight. Fee 0 disables charging (prioritizing stays free).
public record SetPrioritySettingsReq(
  decimal Fee,
  bool AllowAll,
  string? WindowStartSgt,
  string? WindowEndSgt
);

// RESP
public record PrioritySettingsRes(
  decimal Fee,
  bool AllowAll,
  string? WindowStartSgt,
  string? WindowEndSgt
);

public record PriorityAccessRes(string UserId, DateTime CreatedAt);

// may the calling user prioritize right now, and at what fee
public record PriorityEligibilityRes(bool Eligible, decimal Fee);
