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

// the boost ledger page (dd-MM-yyyy inclusive SGT dates; Limit default 50
// max 200, Skip offset)
public record BookingBoostQueryReq(string? After, string? Before, int? Limit, int? Skip);

// KTMB cost: queue a per-direction ticket-cost change; EffectiveAt omitted =
// immediate
public record SetKtmbCostReq(string Direction, decimal Cost, DateTime? EffectiveAt);

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

// TicketsNeeded = Priority + Normal (kept as the total for old argon
// clients; new argon reads the split for the /schedules queue badges)
public record BookingCountRes(
  string Date,
  string Time,
  string Direction,
  int TicketsNeeded,
  int Priority,
  int Normal
);

// total rows matching a search, ignoring Limit/Skip — for real page numbers
public record SearchCountRes(int Total);

// Position/Total (and the PriorityTotal/NormalTotal split of Total) null when
// the booking is no longer queued; Position is 1-based (1 = next to be bought)
public record BookingQueuePositionRes(
  string Status,
  int? Position,
  int? Total,
  int? PriorityTotal,
  int? NormalTotal
);

// ticket reference health: HasRef = the booking carries a ticket key,
// RefValid = that key resolves to a real object in block storage
public record BookingTicketHealthRes(bool HasRef, bool RefValid);

// Sales/revenue analysis. Revenue figures are GROSS; costs are what leaves
// BunnyBooker's pocket: KtmbCost (admin-configured effective-dated ticket
// cost; 0 for every row when never configured) and Airwallex's own gateway
// fees (synced after the fact — see GatewayFeesRes.Coverage). Rows bucket
// completed bookings by the SGT calendar date of CompletedAt (same
// wall-clock convention as booking_stats), direction and departure time;
// GrossRevenue = the booking cost collected at completion.
public record BookingAnalysisRowRes(
  string Date,
  string Direction,
  string Time,
  int TicketsCompleted,
  decimal GrossRevenue,
  decimal KtmbCost
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

// gateway-fee sync coverage over the range's captured intents — fees post
// with delay, so PaymentsWithFee < PaymentsTotal means "sync again later"
public record GatewayFeeCoverageRes(int PaymentsWithFee, int PaymentsTotal);

// Airwallex's own fees: Payments = fees on captured intents (money in),
// Payouts = fees on transfers + card refunds (money out)
public record GatewayFeesRes(decimal Payments, decimal Payouts, GatewayFeeCoverageRes Coverage);

// per-direction slice of the completed rows
public record DirectionBreakdownRes(
  string Direction,
  int Tickets,
  decimal Gross,
  decimal KtmbCost
);

// One SGT calendar month ("MM-yyyy").
// NET = Gross − KtmbCost − GatewayPaymentFees − GatewayPayoutFees.
// InternalFees (deposit + withdrawal + net priority) is BunnyBooker's own
// fee revenue that month — context only, never subtracted.
public record MonthlyAnalysisRes(
  string Month,
  decimal Gross,
  decimal KtmbCost,
  decimal GatewayPaymentFees,
  decimal GatewayPayoutFees,
  decimal InternalFees,
  decimal Net,
  IEnumerable<DirectionBreakdownRes> ByDirection
);

// one pricing component's aggregate: kind = "policy" (signed delta) |
// "discount" (negative) | "priorityFee" (positive) — ranks which components
// make or lose money over the range
public record PriceComponentRes(string Kind, string Name, int TimesApplied, decimal TotalDelta);

// breakdown persistence started with the release that added it — completed
// bookings without a stored breakdown are excluded from Components
public record ComponentsCoverageRes(int WithBreakdown, int Total);

public record BookingAnalysisSummaryRes(
  int TotalTickets,
  decimal TotalGross,
  decimal TotalKtmbCost,
  DepositSummaryRes Deposits,
  InternalFeesRes InternalFees,
  GatewayFeesRes GatewayFees,
  IEnumerable<DirectionBreakdownRes> ByDirection
);

public record BookingAnalysisRes(
  IEnumerable<BookingAnalysisRowRes> Rows,
  BookingAnalysisSummaryRes Summary,
  IEnumerable<MonthlyAnalysisRes> Monthly,
  IEnumerable<PriceComponentRes> Components,
  ComponentsCoverageRes ComponentsCoverage
);

// one boost-ledger row: Fee null = the boost was free; BoostedAt falls back
// to the booking's CreatedAt for boosts predating the PrioritizedAt stamp;
// GrantedBy = the admin's sub when an admin boosted someone else's booking
// (stamped from this release onward, null before and for self-boosts)
public record BookingBoostRes(
  Guid BookingId,
  string UserId,
  string UserIdentity,
  string Date,
  string Time,
  string Direction,
  decimal? Fee,
  bool Free,
  DateTime BoostedAt,
  string? GrantedBy
);

public record BookingBoostPageRes(int Total, IEnumerable<BookingBoostRes> Items);

// KTMB ticket cost: the current per-direction cost + queued future changes
public record KtmbCostChangeRes(
  Guid Id,
  string Direction,
  decimal Cost,
  DateTime EffectiveAt,
  DateTime CreatedAt
);

public record KtmbCostRes(
  // direction name -> current effective cost (0 when never configured)
  Dictionary<string, decimal> Current,
  IEnumerable<KtmbCostChangeRes> Upcoming
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

// REQ: replace the priority settings (insert-only latest, like Cost). THE
// unified system — one ordered policy list, first matching rule decides;
// an empty list means nobody may prioritize.
public record SetPrioritySettingsReq(List<PriorityPolicyReq> Policies);

// One rule. Conditions (unset = wildcard): Target (discount-target shape),
// SGT wall-clock window (HH:mm:ss, may wrap midnight, both bounds or
// neither), hours-to-departure interval [Min, Max). Decision for Allow
// rules: FeeKind "Flat" (SGD) or "Percent" (of the booking's charged ticket
// amount) with FeeValue (0 = free), and an optional per-timeslot SlotCap.
public record PriorityPolicyReq(
  string Name,
  bool Allow,
  DiscountTargetReq? Target = null,
  string? WindowStartSgt = null,
  string? WindowEndSgt = null,
  decimal? MinHoursToDeparture = null,
  decimal? MaxHoursToDeparture = null,
  string FeeKind = "Flat",
  decimal FeeValue = 0,
  int? SlotCap = null
);

// RESP
public record PrioritySettingsRes(List<PriorityPolicyRes> Policies);

public record PriorityPolicyRes(
  string Name,
  bool Allow,
  DiscountTargetRes? Target,
  string? WindowStartSgt,
  string? WindowEndSgt,
  decimal? MinHoursToDeparture,
  decimal? MaxHoursToDeparture,
  string FeeKind,
  decimal FeeValue,
  int? SlotCap
);

public record PriorityAccessRes(string UserId, DateTime CreatedAt);

// QUERY: optional slot context for the generic eligibility check — the
// purchase page knows the timeslot about to be bought before any booking
// exists. All three together (hour-bounded policies + slot cap apply) or
// none (the old any-timeslot answer); formats match the Reserve route
// (WToJ|JToW, dd-MM-yyyy, HH:mm:ss).
public record PriorityEligibilityQuery(
  string? Direction = null,
  string? Date = null,
  string? Time = null
);

// may the calling user prioritize right now, at what fee (null when a
// percent fee cannot be computed without a booking in scope), and free for
// them (Free => Fee is 0). SlotCap/SlotsLeft only when the matched rule caps
// the timeslot (SlotsLeft needs a booking or slot in scope). PolicyName =
// the rule that decided.
public record PriorityEligibilityRes(
  bool Eligible,
  decimal? Fee,
  bool Free,
  int? SlotCap = null,
  int? SlotsLeft = null,
  string? PolicyName = null
);
