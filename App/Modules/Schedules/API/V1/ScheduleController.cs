using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Booking;
using Domain.Schedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Schedules.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class ScheduleController(
  IScheduleService service,
  ScheduleRecordReqValidator scheduleRecordReqValidator,
  ScheduleBulkUpdateReqValidator schedulePrincipalReqValidator,
  ScheduleRangeReqValidator scheduleRangeReqValidator,
  ScheduleDateReqValidator scheduleDateReqValidator,
  AuthHelper authHelper
) : AtomiControllerBase(authHelper)
{
  [HttpGet("latest")]
  public async Task<ActionResult<LatestScheduleRes>> Latest()
  {
    var result = await service.Latest()
      .Then(x => x?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(result,
      new EntityNotFound("There has been no schedules", typeof(Schedule), "latest"));
  }

  [HttpGet("range/{from}/{to}")]
  public async Task<ActionResult<IEnumerable<SchedulePrincipalRes>>> Range([FromRoute] ScheduleRangeReq req)
  {
    var result = await scheduleRangeReqValidator
      .ValidateAsyncResult(req, "Invalid ScheduleRangeReq")
      .ThenAwait(x => service.Range(x.From.ToDate(), x.To.ToDate()))
      .Then(x => x.Select(x => x.ToRes()), Errors.MapAll);
    return this.ReturnResult(result);
  }

  [HttpGet("{date}")]
  public async Task<ActionResult<SchedulePrincipalRes>> Range([FromRoute] ScheduleDateReq req)
  {
    var result = await scheduleDateReqValidator
      .ValidateAsyncResult(req, "Invalid ScheduleGetReq")
      .ThenAwait(x => service.Get(x.Date.ToDate()))
      .Then(x => x.Principal.ToRes(), Errors.MapAll);
    return this.ReturnResult(result);
  }

  [HttpPut("{date}")]
  public async Task<ActionResult<SchedulePrincipalRes>> Update([FromRoute] ScheduleDateReq date,
    [FromBody] ScheduleRecordReq record)
  {
    var result = await scheduleDateReqValidator
      .ValidateAsyncResult(date, "Invalid ScheduleGetReq")
      .ThenAwait(_ => scheduleRecordReqValidator.ValidateAsyncResult(record, "Invalid ScheduleRecordReq"))
      .ThenAwait(_ => service.Update(date.Date.ToDate(), record.ToDomain()))
      .Then(x => x.ToRes(), Errors.MapAll);
    return this.ReturnResult(result);
  }

  [HttpPut("bulk")]
  public async Task<ActionResult> BulkUpdate([FromBody] ScheduleBulkUpdateReq schedules)
  {
    var result = await schedulePrincipalReqValidator
      .ValidateAsyncResult(schedules, "ScheduleBulkUpdateReqValidator")
      .ThenAwait(x => service.BulkUpdate(x.ToDomain()));
    return this.ReturnUnitResult(result);
  }

  [HttpDelete("{date}")]
  public async Task<ActionResult> Delete([FromRoute] ScheduleDateReq date)
  {
    var result = await scheduleDateReqValidator
      .ValidateAsyncResult(date, "Invalid ScheduleGetReq")
      .ThenAwait(x => service.Delete(x.Date.ToDate()));
    return this.ReturnUnitNullableResult(result, new EntityNotFound("Schedule Not Found", typeof(Schedule), date.Date));
  }
}
