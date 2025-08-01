using CSharp_Result;

namespace Domain.Schedule;

public class ScheduleService(IScheduleRepository repo) : IScheduleService
{
  public Task<Result<DateOnly?>> Latest()
  {
    return repo.Latest();
  }

  public Task<Result<IEnumerable<SchedulePrincipal>>> Range(DateOnly start, DateOnly end)
  {
    return repo.Range(start, end);
  }

  public Task<Result<Schedule>> Get(DateOnly date)
  {
    return repo.Get(date)
      .Then(
        x =>
          x
          ?? new Schedule
          {
            Principal = new SchedulePrincipal
            {
              Date = date,
              Record = new ScheduleRecord
              {
                Confirmed = false,
                JToWExcluded = [],
                WToJExcluded = [],
              },
            },
          },
        Errors.MapAll
      );
  }

  public Task<Result<SchedulePrincipal>> Update(DateOnly date, ScheduleRecord record)
  {
    return repo.Update(date, record);
  }

  public Task<Result<Unit>> BulkUpdate(IEnumerable<SchedulePrincipal> record)
  {
    return repo.BulkUpdate(record);
  }

  public Task<Result<Unit?>> Delete(DateOnly date)
  {
    return repo.Delete(date);
  }
}
