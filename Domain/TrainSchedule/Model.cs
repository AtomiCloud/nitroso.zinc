namespace Domain.TrainSchedule;

public record TrainSchedule
{
  public required TrainSchedulePrincipal Principal { get; init; }
}

public record TrainSchedulePrincipal
{
  public required DateOnly Date { get; init; }

  public required TrainScheduleRecord Record { get; init; }
}

public record TrainScheduleRecord
{

  public required bool Confirmed { get; init; }

  public required IEnumerable<TimeOnly> JToWExcluded { get; init; }

  public required IEnumerable<TimeOnly> WToJExcluded { get; init; }
}
