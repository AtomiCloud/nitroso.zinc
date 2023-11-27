using Domain.User;

namespace Domain.Passenger;

public enum PassengerGender
{
  M = 0,
  F = 1,
}

public record PassengerSearch
{
  public string? Name { get; init; }

  public string? UserId { get; init; }

  public int Limit { get; init; }

  public int Skip { get; init; }
}

public record Passenger
{
  public required PassengerPrincipal Principal { get; init; }
  public required UserPrincipal User { get; init; }
}

public record PassengerPrincipal
{
  public required Guid Id { get; init; }

  public required string UserId { get; init; }

  public required PassengerRecord Record { get; init; }
}

public record PassengerRecord
{
  public required string FullName { get; init; }

  public required PassengerGender Gender { get; init; }

  public required DateOnly PassportExpiry { get; init; }

  public required string PassportNumber { get; init; }
}
