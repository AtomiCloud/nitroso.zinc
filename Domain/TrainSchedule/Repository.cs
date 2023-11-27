using CSharp_Result;

namespace Domain.TrainSchedule;

public interface ITrainScheduleRepository
{
  Task<Result<DateOnly>> Latest();

  Task<Result<IEnumerable<TrainSchedule>>> Range(DateOnly start, DateOnly end);

  Task<Result<TrainSchedule?>> Get(DateOnly date);

  Task<Result<TrainSchedulePrincipal>> Update(TrainSchedulePrincipal record);

  Task<Result<IEnumerable<TrainSchedulePrincipal>>> BulkUpdate(IEnumerable<TrainSchedulePrincipal> record);

  Task<Result<Unit?>> Delete(DateOnly date);
}
