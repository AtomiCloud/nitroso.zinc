using App.Modules.Users.API.V1;

namespace App.Modules.Schedules.API.V1;

// REQ
public record ScheduleRangeReq(string From, string To);

public record ScheduleDateReq(string Date);

public record ScheduleRecordReq(bool Confirmed, string[] JToWExcluded, string[] WToJExcluded);

public record SchedulePrincipalReq(string Date, ScheduleRecordReq Record);

public record ScheduleBulkUpdateReq(IEnumerable<SchedulePrincipalReq> Schedules);

// RESP
public record LatestScheduleRes(string Date);

public record SchedulePrincipalRes(string Date, bool Confirmed, string[] JToWExcluded, string[] WToJExcluded);
