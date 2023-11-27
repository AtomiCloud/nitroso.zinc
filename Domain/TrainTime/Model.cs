namespace Domain.TrainTime;

public enum TrainDirection
{
  // JB Sentral to Woodlands
  JToW,
  // Woodlands to JB
  WToJ,
}

public record TrainTiming
{
  public required TrainTimingPrincipal Principal { get; init; }
}

public record TrainTimingPrincipal
{
  public TrainDirection Direction { get; init; }
  public required TrainTimingRecord Record { get; init; }
}

public record TrainTimingRecord
{
  public required IEnumerable<TimeOnly> Timings { get; init; }
}
