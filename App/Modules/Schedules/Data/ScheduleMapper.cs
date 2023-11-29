using Domain.Schedule;

namespace App.Modules.Schedules.Data;

public static class BookingMapper
{
  // to Domain
  public static ScheduleRecord ToRecord(this ScheduleData data) => new()
  {
    Confirmed = data.Confirmed,
    JToWExcluded = data.JToWExcluded,
    WToJExcluded = data.WToJExcluded,
  };

  public static SchedulePrincipal ToPrincipal(this ScheduleData data) => new()
  {
    Date = data.Date,
    Record = data.ToRecord(),
  };


  public static Schedule ToDomain(this ScheduleData data) => new() { Principal = data.ToPrincipal(), };


  // To Data
  public static ScheduleData UpdateData(this ScheduleData data, ScheduleRecord record)
  {
    data.Confirmed = record.Confirmed;
    data.JToWExcluded = record.JToWExcluded.ToArray();
    data.WToJExcluded = record.WToJExcluded.ToArray();
    return data;
  }

  public static ScheduleData ToData(this SchedulePrincipal record)
  {
    return new ScheduleData
    {
      Date = record.Date,
      JToWExcluded = record.Record.JToWExcluded.ToArray(),
      WToJExcluded = record.Record.WToJExcluded.ToArray(),
    };
  }
}
