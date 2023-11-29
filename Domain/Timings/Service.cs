using CSharp_Result;

namespace Domain.Timings;

public class TimingService(ITimingRepository repo) : ITimingService
{
  public Task<Result<Timing?>> Get(TrainDirection direction)
  {
    return repo.Get(direction);
  }

  public Task<Result<TimingPrincipal>> Update(TrainDirection direction, TimingRecord record)
  {
    return repo.Update(direction, record);
  }
}
