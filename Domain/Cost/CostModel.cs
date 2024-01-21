using Domain.Discount;

namespace Domain.Cost;

public record CostPrincipal
{
  public required Guid Id { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required CostRecord Record { get; init; }
}

public record CostRecord
{
  public required decimal Cost { get; init; }
}

public record MaterializedCost
{
  public required decimal Cost { get; init; }
  public required decimal Final { get; init; }

  public required IEnumerable<DiscountRecord> Discounts { get; init; }
}
