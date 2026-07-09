using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using App.Modules.Timings.API.V1;
using Asp.Versioning;
using CSharp_Result;
using Domain.Cost;
using Domain.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Costs.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class CostController(
  ICostService service,
  IUserService userService,
  CreateCostReqValidator costReqValidator,
  CostPolicyReqValidator costPolicyReqValidator,
  CostSummaryQueryValidator costSummaryQueryValidator,
  CostSummaryBatchQueryValidator costSummaryBatchQueryValidator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  // PRICING roles = JWT roles ∪ admin-granted ExtraRoles. The JWT roles are
  // synced/overwritten from Descope, so admin-managed roles live in
  // ExtraRoles; only pricing (discount/policy targeting) reads the union —
  // authorization stays JWT-only everywhere
  private Task<Result<string[]>> PricingRoles(string sub)
  {
    var tokenRoles = h.FieldToScope(HttpContext.User, AuthRoles.Field).ToArray();
    return userService
      .GetById(sub)
      .Then(
        u => tokenRoles.Union(u?.Principal.Record.ExtraRoles ?? []).ToArray(),
        Errors.MapNone
      );
  }

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
    var cost = await this.PricingRoles(sub)
      .ThenAwait(roles => service.Materialize(sub, roles, null))
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
    var cost = await costSummaryQueryValidator
      .ValidateAsyncResult(query, "Invalid CostSummaryQuery")
      .ThenAwait(q => this.PricingRoles(sub).Then(roles => (q, roles), Errors.MapNone))
      .ThenAwait(t => service.Materialize(sub, t.roles, t.q.ToDomain()))
      .Then(x => x.ToSummaryRes(), Errors.MapNone);

    return this.ReturnResult(cost);
  }

  // batch per-slot pricing for the CALLING user: one Date + Direction, up to
  // 100 comma-separated Times — each entry prices exactly like GET summary
  // would for that time
  [Authorize, HttpGet("summary/batch")]
  public async Task<ActionResult<IEnumerable<CostSlotSummaryRes>>> SummaryBatch(
    [FromQuery] CostSummaryBatchQuery query
  )
  {
    var sub = this.Sub()!;
    var x = await costSummaryBatchQueryValidator
      .ValidateAsyncResult(query, "Invalid CostSummaryBatchQuery")
      .ThenAwait(q => this.PricingRoles(sub).Then(roles => (q, roles), Errors.MapNone))
      .ThenAwait(t =>
        service.MaterializeMany(
          sub,
          t.roles,
          t.q.Date.ToDate(),
          t.q.Direction.DirectionToDomain(),
          t.q.ToTimes()
        )
      )
      .Then(s => s.Select(a => a.ToRes()), Errors.MapNone);

    return this.ReturnResult(x);
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
