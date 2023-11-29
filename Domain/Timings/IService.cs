using CSharp_Result;

namespace Domain.Timings;

public interface ITimingService
{
  Task<Result<Timing?>> Get(TrainDirection direction);

  Task<Result<TimingPrincipal>> Update(TrainDirection direction, TimingRecord record);
}
