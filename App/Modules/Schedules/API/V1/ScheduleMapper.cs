using App.Utility;
using Domain.Schedule;

namespace App.Modules.Schedules.API.V1;

public static class ScheduleMapper
{
  // RES
  public static LatestScheduleRes ToRes(this DateOnly date) =>
    new(date.ToStandardDateFormat());

  public static SchedulePrincipalRes ToRes(this SchedulePrincipal principal) =>
    new(principal.Date.ToStandardDateFormat(),
      principal.Record.Confirmed,
      principal.Record.JToWExcluded.Select(t => t.ToStandardTimeFormat()).ToArray(),
      principal.Record.WToJExcluded.Select(t => t.ToStandardTimeFormat()).ToArray()
    );


  // REQ
  public static IEnumerable<SchedulePrincipal> ToDomain(this ScheduleBulkUpdateReq bulk) =>
    bulk.Schedules.Select(s => s.ToDomain());

  public static ScheduleRecord ToDomain(this ScheduleRecordReq record) =>
    new()
    {
      Confirmed = record.Confirmed,
      JToWExcluded = record.JToWExcluded.Select(t => t.ToTime()).ToArray(),
      WToJExcluded = record.WToJExcluded.Select(t => t.ToTime()).ToArray()
    };


  public static SchedulePrincipal ToDomain(this SchedulePrincipalReq principal) =>
    new() { Date = principal.Date.ToDate(), Record = principal.Record.ToDomain(), };
}
