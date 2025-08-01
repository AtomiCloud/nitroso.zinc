namespace Domain.Timings;

public enum TrainDirection
{
  // JB Sentral to Woodlands
  JToW = 0,

  // Woodlands to JB
  WToJ = 1,
}

public record Timing
{
  public required TimingPrincipal Principal { get; init; }
}

public record TimingPrincipal
{
  public TrainDirection Direction { get; init; }
  public required TimingRecord Record { get; init; }
}

public record TimingRecord
{
  public required IEnumerable<TimeOnly> Timings { get; init; }
}
