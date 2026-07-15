using Domain.Passenger;
using Domain.Timings;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;

namespace Domain.Booking;

public enum BookStatus
{
  Pending = 0,

  Buying = 1,
  Completed = 2,

  // end states
  Cancelled = 3,
  Refunded = 4,
  Terminated = 5,

  // duplicate-passport recovery
  Recovering = 6,

  // end state: user already holds this ticket via another channel
  Duplicate = 7,

  // parking state: a booking automation cannot resolve safely (e.g. ledger and
  // status disagree, or recovery exhausted its attempts) — status-only, no
  // money moves; must be resolved by a human, never by automation
  RequireManualIntervention = 8,
}

public record BookingCountSearch
{
  public DateOnly Date { get; init; }

  public TrainDirection Direction { get; init; }
}

public enum BookingSort
{
  // travel date + time, soonest first
  Timing = 0,
  PassengerName = 1,
  PassportNumber = 2,

  // purchase instant (CreatedAt), newest first
  BuyTime = 3,

  // fulfilment instant (CompletedAt), newest first; unfulfilled bookings last
  FulfilTime = 4,
}

public record BookingSearch
{
  public DateOnly? Date { get; init; }

  public TimeOnly? Time { get; init; }

  public BookStatus? Status { get; init; }

  public TrainDirection? Direction { get; init; }

  public string? UserId { get; init; }

  public string? PassportNumber { get; init; }

  // fuzzy (case-insensitive contains) match on the passenger's full name
  public string? PassengerName { get; init; }

  // only bookings whose last entry into Buying is older than this; lets the
  // reverter cron list stuck-Buying bookings without racing live purchases
  public DateTime? BuyingBefore { get; init; }

  // true = only Completed bookings with no ticket file reference (Ticket is
  // NULL) — the ticket-repair worklist; DB-level filter only, storage is
  // never probed per row
  public bool? MissingTicket { get; init; }

  // null = travel-date descending (the historical default ordering)
  public BookingSort? Sort { get; init; }

  public int Limit { get; init; }

  public int Skip { get; init; }
}

public record Booking
{
  public required BookingPrincipal Principal { get; init; }
  public required UserPrincipal User { get; init; }
  public required TransactionPrincipal Transaction { get; init; }

  public required WalletPrincipal Wallet { get; init; }
}

public record BookingPrincipal
{
  public required Guid Id { get; init; }

  public required string UserId { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required BookingRecord Record { get; init; }

  public required BookingStatus Status { get; init; }

  public required BookingComplete Complete { get; init; }

  // priority bookings jump to the front of their timeslot's purchase queue
  // (defaults keep pre-priority construction sites valid)
  public bool Priority { get; init; }

  // snapshot of the fee charged when the booking was prioritized; refunded
  // to Usable when the booking ends Refunded or Cancelled
  public decimal? PriorityFee { get; init; }

  // how many times this booking was recycled from 'Recovering' back to
  // 'Pending' for another purchase attempt; RecoverRevert stops at the
  // configured cap so a permanently-conflicted booking cannot loop forever
  // (default keeps pre-retry construction sites valid)
  public int RecoveryRetries { get; init; }
}

public record BookingComplete
{
  public required string? Ticket { get; init; } = null;

  public required string? BookingNumber { get; init; } = null;

  public required string? TicketNumber { get; init; } = null;

  // what BunnyBooker ACTUALLY paid KTMB for this ticket, captured by tin at
  // completion (or backfilled after the fact). Both set together or both
  // null; null = not recorded (the analysis then costs the booking at the
  // admin-entered per-direction estimate). Never overwritten once recorded —
  // the amount is a fact about the KTMB purchase.
  public decimal? KtmbAmount { get; init; }

  // ISO currency of KtmbAmount ("MYR" or "SGD")
  public string? KtmbCurrency { get; init; }

  // what KTMB ACTUALLY refunded BunnyBooker when the booking was terminated,
  // captured by tin's terminator (or backfilled after the fact). Both set
  // together or both null; null = not recorded. Unlike KtmbAmount this is an
  // upsert — a repeated capture overwrites the stored values.
  public decimal? KtmbRefundAmount { get; init; }

  // ISO currency of KtmbRefundAmount (e.g. "MYR")
  public string? KtmbRefundCurrency { get; init; }
}

// the actual KTMB-paid cost of one booking (see BookingComplete.KtmbAmount)
public record BookingKtmbCost
{
  public required decimal Amount { get; init; }

  public required string Currency { get; init; }
}

// the actual KTMB termination refund of one booking (see
// BookingComplete.KtmbRefundAmount)
public record BookingKtmbRefund
{
  public required decimal Amount { get; init; }

  public required string Currency { get; init; }
}

// one row of the tin backfill worklist: a Completed booking that captured a
// KTMB reservation (both identifiers present) but has no actual paid amount
// recorded yet
public record BookingKtmbCostMissing
{
  public required Guid Id { get; init; }

  public required string BookingNo { get; init; }

  public required string TicketNo { get; init; }

  public required DateTime CompletedAt { get; init; }
}

public record BookingStatus
{
  public required BookStatus Status { get; init; } = BookStatus.Pending;

  public required DateTime? CompletedAt { get; init; } = null;

  // when the booking last entered Buying; stamped by the data layer on the
  // Pending -> Buying transition (read-only for callers)
  public DateTime? LastBuyingAt { get; init; }
}

public record BookingRecord
{
  public required DateOnly Date { get; init; }

  public required TimeOnly Time { get; init; }

  public required TrainDirection Direction { get; init; }

  public required PassengerRecord Passenger { get; init; }
}

public record BookingCount
{
  public required DateOnly Date { get; init; }

  public required TimeOnly Time { get; init; }

  public required TrainDirection Direction { get; init; }

  // total pending bookings in the slot (Priority + Normal — kept as the
  // backward-compatible field old argon clients read)
  public required int TicketsNeeded { get; init; }

  // the split behind TicketsNeeded: priority-boosted bookings (front of the
  // purchase queue) vs the normal queue
  public required int Priority { get; init; }

  public required int Normal { get; init; }
}

// One pricing component applied at purchase: a cost policy's signed delta
// (vs the base cost) or a discount's negative delta (vs the subtotal)
public record BookingPriceLine
{
  // "policy" | "discount"
  public required string Kind { get; init; }

  public required string Name { get; init; }

  public required decimal Delta { get; init; }
}

// The price breakdown captured at purchase, so analytics can rank which
// policies/discounts make or lose money. BaseCost + policy lines = the
// subtotal; subtotal + discount lines ≈ Final (Final additionally floors at
// 0 and rounds to the stored precision). Persisted from the release that
// introduced it — older bookings have none.
public record BookingPriceBreakdown
{
  public required decimal BaseCost { get; init; }

  public required BookingPriceLine[] Lines { get; init; }

  public required decimal Final { get; init; }
}

// Cheap single-booking probe of the ticket file reference: HasRef = the
// booking carries a Ticket key; RefValid = that key resolves to a real object
// in block storage (false when the reference dangles, e.g. a lost upload)
public record BookingTicketHealth
{
  public required bool HasRef { get; init; }

  public required bool RefValid { get; init; }
}

// A booking's place in its timeslot's purchase queue. Position/Total (and the
// PriorityTotal/NormalTotal split of Total) are null when the booking is no
// longer queued (completed, refunded, cancelled, ...). Position is 1-based:
// 1 = next to be bought.
public record BookingQueuePosition
{
  public required BookStatus Status { get; init; }

  public required int? Position { get; init; }

  public required int? Total { get; init; }

  public required int? PriorityTotal { get; init; }

  public required int? NormalTotal { get; init; }
}

public record BookingStatsQuery
{
  // travel-date range (inclusive); null = unbounded
  public DateOnly? After { get; init; }

  public DateOnly? Before { get; init; }
}

// Booking outcomes bucketed by everything the success-rate dashboards slice
// on: day of week (of travel), departure time, direction, priority, how long
// before departure the booking was made, how contested the slot was, and how
// long before departure completed bookings were delivered. The UI aggregates
// across any of these. Backed by the booking_stats materialized view.
public record BookingStatRow
{
  public required DayOfWeek DayOfWeek { get; init; }

  public required TimeOnly Time { get; init; }

  public required TrainDirection Direction { get; init; }

  public required bool Priority { get; init; }

  // lead time from purchase to departure:
  // 6h, 12h, 24h, 2d, 3d, 4d, 1w, 2w, 3w, 4w, 1m, 2m, 3m, 6m, 6m+
  public required string Bucket { get; init; }

  // bookings sharing the same date+time+direction slot instance (any status):
  // 0-5, 5-10, 10-20, 20-30, 30+
  public required string DemandBucket { get; init; }

  // delivery-to-departure time for completed bookings:
  // 1h, 2h, 3h, 4h, 5h, 6h, 12h, 24h, 48h, 48h+; null when not completed
  public required string? DeliveryBucket { get; init; }

  public required int Total { get; init; }

  public required int Completed { get; init; }

  public required int Refunded { get; init; }

  public required int Cancelled { get; init; }

  public required int Terminated { get; init; }

  // everything else: still in flight (Pending/Buying/Recovering), Duplicate,
  // RequireManualIntervention
  public required int Other { get; init; }
}
