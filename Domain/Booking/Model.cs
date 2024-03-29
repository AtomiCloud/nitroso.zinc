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
}

public record BookingCountSearch
{
  public DateOnly Date { get; init; }

  public TrainDirection Direction { get; init; }
}

public record BookingSearch
{
  public DateOnly? Date { get; init; }

  public TimeOnly? Time { get; init; }

  public BookStatus? Status { get; init; }

  public TrainDirection? Direction { get; init; }

  public string? UserId { get; init; }

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
