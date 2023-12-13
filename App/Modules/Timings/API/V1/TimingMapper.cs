using App.Utility;
using CSharp_Result;
using Domain.Schedule;
using Domain.Timings;

namespace App.Modules.Timings.API.V1;

public static class TimingMapper
{
  // Domain -> RES
  public static string ToRes(this TrainDirection direction) => direction switch
  {
    TrainDirection.JToW => "JToW",
    TrainDirection.WToJ => "WToJ",
    _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Invalid direction")
  };

  public static TimingPrincipalRes ToRes(this TimingPrincipal principal) =>
    new(principal.Direction.ToRes(),
      principal.Record.Timings.Select(x => x.ToStandardTimeFormat()
      ).ToArray()
    );

  public static TimingRes ToRes(this Timing timing) =>
    new(timing.Principal.ToRes());

  // REQ -> Domain
  public static TrainDirection DirectionToDomain(this string direction) => direction switch
  {
    "JToW" => TrainDirection.JToW,
    "WToJ" => TrainDirection.WToJ,
    _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Invalid direction")
  };

  public static TrainDirection ToDomain(this TrainDirectionReq req)
    => req.Direction.DirectionToDomain();

  public static TimingRecord ToRecord(this TimingReq req) =>
    new() { Timings = req.Timings.Select(x => x.ToTime()) };
}
