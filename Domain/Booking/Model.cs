using Domain.Passenger;
using Domain.User;

namespace Domain.Booking;

public enum BookStatus
{
  Pending = 0,
  Completed = 1,
  Cancelled = 2,
}

public record BookingSearch
{
  public DateOnly? Date { get; init; }

  public TimeOnly? Time { get; init; }

  public string? UserId { get; init; }

  public int Limit { get; init; }

  public int Skip { get; init; }
}

public record Booking
{
  public required BookingPrincipal Principal { get; init; }
  public required UserPrincipal User { get; init; }
}

public record BookingPrincipal
{
  public required Guid Id { get; init; }

  public required string UserId { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required BookingRecord Record { get; init; }

  public required BookingStatus Status { get; init; }
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

  public required IEnumerable<PassengerRecord> Passengers { get; init; }
}

public record BookingCount
{
  public required DateOnly Date { get; init; }

  public required TimeOnly Time { get; init; }

  public required int TicketsNeeded { get; init; }
}
