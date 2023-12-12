using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Timings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Timings.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class TimingController(
  ITimingService service,
  TrainDirectionReqValidator trainDirectionReqValidator,
  TimingReqValidator timingReqValidator,
  IAuthHelper authHelper
) : AtomiControllerBase(authHelper)
{

  [HttpGet("{Direction}")]
  public async Task<ActionResult<TimingRes>> Get([FromRoute] TrainDirectionReq req)
  {
    var result = await trainDirectionReqValidator.ValidateAsyncResult(req, "Invalid TrainDirectionReq")
      .ThenAwait(v => service.Get(v.ToDomain()))
      .Then(x => x?.ToRes(), Errors.MapAll);

    var notfound = new EntityNotFound("Timing not found", typeof(Timing), req.Direction);
    return this.ReturnNullableResult(result, notfound);
  }


  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPut("{Direction}")]
  public async Task<ActionResult<TimingPrincipalRes>> Update([FromRoute] TrainDirectionReq req, [FromBody] TimingReq body)
  {
    var result = await trainDirectionReqValidator.ValidateAsyncResult(req, "Invalid TrainDirectionReq")
      .ThenAwait(_ => timingReqValidator.ValidateAsyncResult(body, "Invalid TimingReq"))
      .ThenAwait(v => service.Update(req.ToDomain(), v.ToRecord()))
      .Then(x => x.ToRes(), Errors.MapAll);

    return this.ReturnResult(result);
  }


}
