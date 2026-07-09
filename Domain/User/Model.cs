using Domain.Wallet;

namespace Domain.User;

public record UserSearch
{
  public string? Id { get; init; }

  public string? Username { get; init; }
  
  public string? Email { get; init; }

  public int Limit { get; init; }

  public int Skip { get; init; }
}

public record User
{
  public required UserPrincipal Principal { get; init; }

  public required WalletPrincipal Wallet { get; init; }
}

public record UserPrincipal
{
  public required string Id { get; init; }
  public required UserRecord Record { get; init; }
}

public record UserRecord
{
  public required string Username { get; init; }
  public string? Email { get; init; }
  public bool? EmailVerified { get; init; }
  public string[]? Roles { get; init; }

  // Admin-managed roles for discount/pricing targeting. Roles above mirrors
  // the Descope JWT and is overwritten by the frontend token sync, so
  // admin-granted roles live here instead; only mutated via the dedicated
  // add/remove role flows (the data-layer Update preserves it) and never
  // used for authorization
  public string[] ExtraRoles { get; init; } = [];
}
