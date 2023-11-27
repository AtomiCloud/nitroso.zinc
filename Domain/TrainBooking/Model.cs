using Domain.Passenger;
using Domain.User;

namespace Domain.TrainBooking;

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
}

public record BookingStatus
{
  public required bool Completed { get; init; } = false;

  public required DateTime? CompletedAt { get; init; } = null;
}

public record BookingRecord
{
  public required DateOnly Date { get; init; }

  public required TimeOnly Time { get; init; }

  public required IEnumerable<PassengerRecord> Passenger { get; init; }
}

public record BookingPoll
{
  public required DateOnly Date { get; init; }

  public required IEnumerable<TimeOnly> Timings { get; init; }

  public BookingPoll After(TimeOnly time)
  {
    return this with { Timings = this.Timings.Where(x => x >= time), };
  }
}
