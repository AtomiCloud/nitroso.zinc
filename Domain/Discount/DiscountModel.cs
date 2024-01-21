namespace Domain.Discount;

public record DiscountSearch
{
  public string? Search { get; init; }
  public DiscountType? DiscountType { get; init; }
  public DiscountMatchMode? MatchMode { get; init; }
  public string[]? MatchTarget { get; init; }

  public bool? Disabled { get; init; }
}

public enum DiscountType
{
  Percentage,
  Flat,
}

public enum DiscountMatchMode
{
  All,
  Any,
  None,
}

public enum DiscountMatchType
{
  UserId,
  Role,
}

public record DiscountMatch
{
  public string Value { get; init; } = string.Empty;
  public DiscountMatchType Type { get; init; }
}

public record DiscountPrincipal
{
  public required Guid Id { get; init; }

  public required DiscountRecord Record { get; init; }

  public required DiscountTarget Target { get; init; }

  public required DiscountStatus Status { get; init; }
}

public record DiscountStatus
{
  public required bool Disabled { get; init; }
}

public record DiscountTarget
{
  public required DiscountMatchMode MatchMode { get; init; }

  public required IEnumerable<DiscountMatch> Matches { get; init; }
}

public record DiscountRecord
{
  public required string Name { get; init; }

  public required string Description { get; init; }

  public required decimal Amount { get; init; }

  public required DiscountType Type { get; init; }
}
