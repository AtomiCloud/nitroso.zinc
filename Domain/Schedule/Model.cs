namespace Domain.Schedule;

public record Schedule
{
  public required SchedulePrincipal Principal { get; init; }
}

public record SchedulePrincipal
{
  public required DateOnly Date { get; init; }

  public required ScheduleRecord Record { get; init; }
}

public record ScheduleRecord
{

  public required bool Confirmed { get; init; }

  public required IEnumerable<TimeOnly> JToWExcluded { get; init; }

  public required IEnumerable<TimeOnly> WToJExcluded { get; init; }
}
