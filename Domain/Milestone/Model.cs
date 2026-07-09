namespace Domain.Milestone;

// A dated marker for snatching-algorithm changes: the stats UI defaults its
// range start to the latest milestone so success rates are read against the
// algorithm that produced them.
public record MilestoneRecord
{
  // the (travel-calendar) date the change took effect
  public required DateOnly Date { get; init; }

  public required string Label { get; init; }
}

public record MilestonePrincipal
{
  public required Guid Id { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required MilestoneRecord Record { get; init; }
}
