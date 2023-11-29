using Domain.Timings;

namespace App.Modules.Timings.Data;

public static class TimingMapper
{
  // to Domain
  public static TimingRecord ToRecord(this TimingData data) => new() { Timings = data.Timings, };

  public static TimingPrincipal ToPrincipal(this TimingData data) => new()
  {
    Direction = data.Direction.ToTrainDirection(),
    Record = data.ToRecord(),
  };

  public static Timing ToDomain(this TimingData data) => new()
  {
    Principal = data.ToPrincipal(),
  };

  public static TrainDirection ToTrainDirection(this int d) => d switch
  {
    0 => TrainDirection.JToW,
    1 => TrainDirection.WToJ,
    _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Invalid direction"),
  };

  // To Data
  public static int ToData(this TrainDirection direction) =>
    direction switch
    {
      TrainDirection.JToW => 0,
      TrainDirection.WToJ => 1,
      _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Invalid direction"),
    };

  public static TimingData ToData(this TimingRecord record) => new()
  {
    Timings = record.Timings.ToArray(),
  };

  public static TimingData UpdateData(this TimingData data, TimingRecord record)
  {
    data.Timings = record.Timings.ToArray();
    return data;
  }

}
