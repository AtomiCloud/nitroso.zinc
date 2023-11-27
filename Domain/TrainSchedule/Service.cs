using CSharp_Result;

namespace Domain.TrainSchedule;

public class TrainScheduleService(ITrainScheduleRepository repo) : ITrainScheduleService
{
  public Task<Result<DateOnly>> Latest()
  {
    return repo.Latest();
  }

  public Task<Result<IEnumerable<TrainSchedule>>> Range(DateOnly start, DateOnly end)
  {
    return repo.Range(start, end);
  }

  public Task<Result<TrainSchedule>> Get(DateOnly date)
  {
    return repo.Get(date)
      .Then(
        x => x ?? new TrainSchedule
        {
          Principal = new TrainSchedulePrincipal
          {
            Date = date,
            Record = new TrainScheduleRecord { Confirmed = false, JToWExcluded = [], WToJExcluded = [], }
          }
        }, Errors.MapAll);
  }

  public Task<Result<TrainSchedulePrincipal>> Update(TrainSchedulePrincipal record)
  {
    return repo.Update(record);
  }

  public Task<Result<IEnumerable<TrainSchedulePrincipal>>> BulkUpdate(IEnumerable<TrainSchedulePrincipal> record)
  {
    return repo.BulkUpdate(record);
  }

  public Task<Result<Unit?>> Delete(DateOnly date)
  {
    return repo.Delete(date);
  }
}
