using App.Modules.Users.API.V1;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Schedules.API.V1;

// REQ
public record ScheduleRangeReq([FromRoute] string From, [FromRoute] string To);

public record ScheduleDateReq([FromRoute] string Date);

public record ScheduleRecordReq(bool Confirmed, string[] JToWExcluded, string[] WToJExcluded);

public record SchedulePrincipalReq(string Date, ScheduleRecordReq Record);

public record ScheduleBulkUpdateReq(IEnumerable<SchedulePrincipalReq> Schedules);

// RESP
public record LatestScheduleRes(string Date);

public record SchedulePrincipalRes(string Date, bool Confirmed, string[] JToWExcluded, string[] WToJExcluded);
