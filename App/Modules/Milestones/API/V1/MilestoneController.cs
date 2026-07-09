using System.Net.Mime;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain;
using Domain.Milestone;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Milestones.API.V1;

// Milestones mark snatching-algorithm changes; admins manage them and the
// stats page defaults its range start to the latest one, so success rates
// are read against the algorithm that produced them.
[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class MilestoneController(
  IMilestoneRepository repo,
  CreateMilestoneReqValidator createMilestoneReqValidator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  // newest Date first; any authed user — the stats page needs it
  [Authorize, HttpGet]
  public async Task<ActionResult<IEnumerable<MilestoneRes>>> List()
  {
    var x = await repo.List().Then(ms => ms.Select(m => m.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost]
  public async Task<ActionResult<MilestoneRes>> Add([FromBody] CreateMilestoneReq req)
  {
    var x = await createMilestoneReqValidator
      .ValidateAsyncResult(req, "Invalid CreateMilestoneReq")
      .ThenAwait(r => repo.Add(r.ToRecord()))
      .Then(m => m.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("{id:guid}")]
  public async Task<ActionResult<MilestoneRes>> Delete(Guid id)
  {
    var x = await repo
      .Delete(id)
      .NullToError(id.ToString())
      .Then(m => m.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }
}
