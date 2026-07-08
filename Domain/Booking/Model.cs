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
}

public record BookingSearch
{
  public DateOnly? Date { get; init; }

  public TimeOnly? Time { get; init; }

  public BookStatus? Status { get; init; }

  public TrainDirection? Direction { get; init; }

  public string? UserId { get; init; }

  public string? PassportNumber { get; init; }

  // only bookings whose last entry into Buying is older than this; lets the
  // reverter cron list stuck-Buying bookings without racing live purchases
  public DateTime? BuyingBefore { get; init; }

  // null = created-date descending (the historical default)
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
}

public record BookingComplete
{
  public required string? Ticket { get; init; } = null;

  public required string? BookingNumber { get; init; } = null;

  public required string? TicketNumber { get; init; } = null;
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
  public required int TicketsNeeded { get; init; }
}
