using CSharp_Result;

namespace Domain.Schedule;

public interface IScheduleService
{
  Task<Result<DateOnly?>> Latest();

  Task<Result<IEnumerable<SchedulePrincipal>>> Range(DateOnly start, DateOnly end);

  Task<Result<Schedule>> Get(DateOnly date);

  public Task<Result<SchedulePrincipal>> Update(DateOnly date, ScheduleRecord record);

  Task<Result<Unit>> BulkUpdate(IEnumerable<SchedulePrincipal> record);

  Task<Result<Unit?>> Delete(DateOnly date);
}
