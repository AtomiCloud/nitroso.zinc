using CSharp_Result;

namespace Domain.TrainTime;

public interface ITrainTimingService
{
  Task<Result<TrainTiming>> Get(TrainDirection direction);

  Task<Result<TrainTimingPrincipal>> Update(TrainDirection direction, TrainTimingRecord record);
}
