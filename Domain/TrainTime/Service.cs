using CSharp_Result;

namespace Domain.TrainTime;

public class TrainTimingService(ITrainTimingRepository repo) : ITrainTimingService
{
  public Task<Result<TrainTiming>> Get(TrainDirection direction)
  {
    return repo.Get(direction);
  }

  public Task<Result<TrainTimingPrincipal>> Update(TrainDirection direction, TrainTimingRecord record)
  {
    return repo.Update(direction, record);
  }
}
