using System.Net.Mime;
using App.Modules.Common;
using App.Modules.Wallets.API.V1;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Admin.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class AdminController(
  IAdminService service,
  TransferReqValidator transferReqValidator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("inflow/{userId}")]
  public async Task<ActionResult<WalletPrincipalRes>> TransferIn(
    string userId,
    [FromBody] TransferReq req
  )
  {
    var x = await transferReqValidator
      .ValidateAsyncResult(req, "Invalid TransferReq")
      .ThenAwait(q => service.TransferIn(userId, q.Amount, q.Desc))
      .Then(x => x.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("outflow/{userId}")]
  public async Task<ActionResult<WalletPrincipalRes>> TransferOut(
    string userId,
    [FromBody] TransferReq req
  )
  {
    var x = await transferReqValidator
      .ValidateAsyncResult(req, "Invalid TransferReq")
      .ThenAwait(q => service.TransferOut(userId, q.Amount, q.Desc))
      .Then(x => x.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("promo/{userId}")]
  public async Task<ActionResult<WalletPrincipalRes>> PromotionalCredit(
    string userId,
    [FromBody] TransferReq req
  )
  {
    var x = await transferReqValidator
      .ValidateAsyncResult(req, "Invalid TransferReq")
      .ThenAwait(q => service.Promo(userId, q.Amount, q.Desc))
      .Then(x => x.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }
}
