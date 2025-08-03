using Domain.Timings;

namespace Domain;

public static class Humanize
{
  public static string ToHuman(this DateOnly date) => date.ToString("dd MMMM yyyy");

  public static string ToHuman(this TimeOnly time) => time.ToString("h:mm tt");

  public static string ToHuman(this TrainDirection direction) =>
    direction switch
    {
      TrainDirection.JToW => "JB Sentral to Woodlands Checkpoint",
      TrainDirection.WToJ => "Woodlands Checkpoint to JB Sentral",
      _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };
}
