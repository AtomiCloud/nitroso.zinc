namespace Domain.User;

public record UserSearch
{
  public string? Id { get; init; }

  public string? Username { get; init; }

  public int Limit { get; init; }

  public int Skip { get; init; }
}

public record User
{
  public required UserPrincipal Principal { get; init; }
}

public record UserPrincipal
{
  public required string Id { get; init; }
  public required UserRecord Record { get; init; }
}

public record UserRecord
{
  public required string Username { get; init; }
}
