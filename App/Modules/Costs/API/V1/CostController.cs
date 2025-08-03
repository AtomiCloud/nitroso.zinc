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
    var cost = await service.Materialize(sub, role.ToArray()).Then(x => x?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(
      cost,
      new EntityNotFound("Cost Not Found", typeof(CostPrincipal), "none")
    );
  }
}
