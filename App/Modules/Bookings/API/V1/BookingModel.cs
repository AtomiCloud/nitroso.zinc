using App.Modules.Discounts.API.V1;
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

// sales/revenue analysis range (dd-MM-yyyy, inclusive SGT dates; null =
// unbounded) — the admin Analysis page source
public record BookingAnalysisQueryReq(string? After, string? Before);

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

// Sales/revenue analysis. GROSS figures + BunnyBooker's internal fees only:
// Airwallex's own gateway fees are not stored anywhere (no API for them yet).
// Rows bucket completed bookings by the SGT calendar date of CompletedAt
// (same wall-clock convention as booking_stats), direction and departure
// time; GrossRevenue = the booking cost collected at completion.
public record BookingAnalysisRowRes(
  string Date,
  string Direction,
  string Time,
  int TicketsCompleted,
  decimal GrossRevenue
);

public record DepositSummaryRes(int Count, decimal Captured);

// Priority is NET (charges minus refunds); Termination = collected cost of
// terminated bookings minus what was refunded
public record InternalFeesRes(
  decimal Deposit,
  decimal Withdrawal,
  decimal Priority,
  decimal Termination
);

public record BookingAnalysisSummaryRes(
  int TotalTickets,
  decimal TotalGross,
  DepositSummaryRes Deposits,
  InternalFeesRes InternalFees
);

public record BookingAnalysisRes(
  IEnumerable<BookingAnalysisRowRes> Rows,
  BookingAnalysisSummaryRes Summary
);

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
// FreeTarget/AccessTarget use the SAME shape as the Discounts API targets:
// FreeTarget = who boosts free (fee 0, no ledger row); AccessTarget = who may
// prioritize at all — when set it takes precedence over AllowAll/the
// allowlist; null keeps legacy behavior.
public record SetPrioritySettingsReq(
  decimal Fee,
  bool AllowAll,
  string? WindowStartSgt,
  string? WindowEndSgt,
  DiscountTargetReq? FreeTarget = null,
  DiscountTargetReq? AccessTarget = null
);

// RESP
public record PrioritySettingsRes(
  decimal Fee,
  bool AllowAll,
  string? WindowStartSgt,
  string? WindowEndSgt,
  DiscountTargetRes? FreeTarget,
  DiscountTargetRes? AccessTarget
);

public record PriorityAccessRes(string UserId, DateTime CreatedAt);

// may the calling user prioritize right now, at what fee, and free for them
// (Free => Fee is 0)
public record PriorityEligibilityRes(bool Eligible, decimal Fee, bool Free);
