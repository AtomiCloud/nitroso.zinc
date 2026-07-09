using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Cost;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Costs.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class CostController(
  ICostService service,
  CreateCostReqValidator costReqValidator,
  CostPolicyReqValidator costPolicyReqValidator,
  CostSummaryQueryValidator costSummaryQueryValidator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet]
  public async Task<ActionResult<IEnumerable<CostPrincipalRes>>> History()
  {
    var x = await service.History().Then(x => x.Select(a => a.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost]
  public async Task<ActionResult<CostPrincipalRes>> Create([FromBody] CreateCostReq req)
  {
    var cost = await costReqValidator
      .ValidateAsyncResult(req, "Invalid CreateCostReq")
      .ThenAwait(x => service.Create(x.ToDomain()))
      .Then(x => x.ToRes(), Errors.MapNone);

    return this.ReturnResult(cost);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("current")]
  public async Task<ActionResult<CostPrincipalRes>> GetCurrent()
  {
    var cost = await service.GetCurrent().Then(x => x?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(
      cost,
      new EntityNotFound("Cost Not Found", typeof(CostPrincipal), "none")
    );
  }

  [Authorize, HttpGet("self")]
  public async Task<ActionResult<MaterializedCostRes>> Self()
  {
    var sub = this.Sub()!;
    var role = h.FieldToScope(HttpContext.User, AuthRoles.Field);
    var cost = await service
      .Materialize(sub, role.ToArray(), null)
      .Then(x => x?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(
      cost,
      new EntityNotFound("Cost Not Found", typeof(CostPrincipal), "none")
    );
  }

  // the full price breakdown for one booking spec, for the CALLING user —
  // powers the purchase page and the admin costs page live preview
  [Authorize, HttpGet("summary")]
  public async Task<ActionResult<CostSummaryRes>> Summary([FromQuery] CostSummaryQuery query)
  {
    var sub = this.Sub()!;
    var role = h.FieldToScope(HttpContext.User, AuthRoles.Field);
    var cost = await costSummaryQueryValidator
      .ValidateAsyncResult(query, "Invalid CostSummaryQuery")
      .ThenAwait(q => service.Materialize(sub, role.ToArray(), q.ToDomain()))
      .Then(x => x.ToSummaryRes(), Errors.MapNone);

    return this.ReturnResult(cost);
  }

  // Policies: pricing rules that add signed deltas onto the base cost
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("policies")]
  public async Task<ActionResult<IEnumerable<CostPolicyPrincipalRes>>> ListPolicies()
  {
    var x = await service.ListPolicies().Then(p => p.Select(a => a.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("policies")]
  public async Task<ActionResult<CostPolicyPrincipalRes>> CreatePolicy(
    [FromBody] CostPolicyReq req
  )
  {
    var x = await costPolicyReqValidator
      .ValidateAsyncResult(req, "Invalid CostPolicyReq")
      .ThenAwait(r => service.CreatePolicy(r.ToDomain()))
      .Then(p => p.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPut("policies/{id:guid}")]
  public async Task<ActionResult<CostPolicyPrincipalRes>> UpdatePolicy(
    Guid id,
    [FromBody] CostPolicyReq req
  )
  {
    var x = await costPolicyReqValidator
      .ValidateAsyncResult(req, "Invalid CostPolicyReq")
      .ThenAwait(r => service.UpdatePolicy(id, r.ToDomain()))
      .Then(p => p?.ToRes(), Errors.MapNone);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Cost Policy Not Found", typeof(CostPolicyPrincipal), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("policies/{id:guid}")]
  public async Task<ActionResult> DeletePolicy(Guid id)
  {
    var x = await service.DeletePolicy(id);
    return this.ReturnUnitNullableResult(
      x,
      new EntityNotFound("Cost Policy Not Found", typeof(CostPolicyPrincipal), id.ToString())
    );
  }
}
