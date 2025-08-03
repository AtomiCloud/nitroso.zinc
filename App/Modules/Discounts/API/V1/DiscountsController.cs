using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Discount;
using Domain.Wallet;
using Domain.Withdrawal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Discounts.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class DiscountController(
  IDiscountService service,
  DiscountSearchQueryValidator searchQueryValidator,
  CreateDiscountReqValidator createDiscountReqValidator,
  UpdateDiscountReqValidator updateDiscountReqValidator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet]
  public async Task<ActionResult<IEnumerable<DiscountPrincipalRes>>> Search(
    [FromQuery] DiscountSearchQuery query
  )
  {
    var x = await searchQueryValidator
      .ValidateAsyncResult(query, "Invalid DiscountSearchQuery")
      .Then(x => x.ToDomain(), Errors.MapNone)
      .ThenAwait(q => service.Search(q))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("{id:guid}")]
  public async Task<ActionResult<DiscountPrincipalRes>> Get(Guid id)
  {
    var discount = await service.Get(id).Then(x => x?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(
      discount,
      new EntityNotFound("Discount Not Found", typeof(DiscountPrincipal), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost]
  public async Task<ActionResult<DiscountPrincipalRes>> Create([FromBody] CreateDiscountReq req)
  {
    var discount = await createDiscountReqValidator
      .ValidateAsyncResult(req, "Invalid CreateDiscountReq")
      .ThenAwait(x => service.Create(x.Record.ToDomain(), x.Target.ToDomain()))
      .Then(x => x.ToRes(), Errors.MapNone);
    return this.ReturnResult(discount);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPut("{id:guid}")]
  public async Task<ActionResult<DiscountPrincipalRes>> Update(
    Guid id,
    [FromBody] UpdateDiscountReq req
  )
  {
    var discount = await updateDiscountReqValidator
      .ValidateAsyncResult(req, "Invalid UpdateDiscountReq")
      .ThenAwait(x =>
        service.Update(id, x.Status.ToDomain(), x.Record.ToDomain(), x.Target.ToDomain())
      )
      .Then(x => x?.ToRes(), Errors.MapNone);
    return this.ReturnNullableResult(
      discount,
      new EntityNotFound("Discount Not Found", typeof(DiscountPrincipal), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("{id:guid}")]
  public async Task<ActionResult> Delete(Guid id)
  {
    var user = await service.Delete(id);
    return this.ReturnUnitNullableResult(
      user,
      new EntityNotFound("Discount Not Found", typeof(DiscountPrincipal), id.ToString())
    );
  }
}
