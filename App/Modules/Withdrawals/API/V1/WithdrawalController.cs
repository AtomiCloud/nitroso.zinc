using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Wallet;
using Domain.Withdrawal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Withdrawals.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class WithdrawalController(
  IWithdrawalService service,
  CreateWithdrawalReqValidator createWithdrawalReqValidator,
  RejectWithdrawalReqValidator rejectWithdrawalReqValidator,
  CancelWithdrawalReqValidator cancelWithdrawalReqValidator,
  SearchWithdrawalQueryValidator searchWithdrawalQueryValidator,
  IWithdrawalImageEnricher enrich,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  [Authorize, HttpGet]
  public async Task<ActionResult<IEnumerable<WithdrawalPrincipalRes>>> Search(
    [FromQuery] SearchWithdrawalQuery query
  )
  {
    var x = await this.GuardOrAllAsync(query.UserId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ =>
        searchWithdrawalQueryValidator.ValidateAsyncResult(query, "Invalid SearchWithdrawalQuery")
      )
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapNone)
      .ThenAwait(x => enrich.Enrich(x));

    return this.ReturnResult(x);
  }

  [Authorize, HttpGet("{id:guid}")]
  public async Task<ActionResult<WithdrawalRes>> Get(Guid id, string? userId)
  {
    var wallet = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.Get(id, userId))
      .Then(x => x?.ToRes(), Errors.MapNone)
      .ThenAwait(x => Utility.Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));

    return this.ReturnNullableResult(
      wallet,
      new EntityNotFound("Wallet Not Found", typeof(Wallet), id.ToString())
    );
  }

  [Authorize, HttpPost("{userId}")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Create(
    string userId,
    [FromBody] CreateWithdrawalReq req
  )
  {
    var withdrawal = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ =>
        createWithdrawalReqValidator.ValidateAsyncResult(req, "Invalid CreateWithdrawalReq")
      )
      .ThenAwait(r => service.Create(userId, r.ToDomain()))
      .Then(x => x.ToRes(), Errors.MapNone)
      .ThenAwait(x => enrich.Enrich(x));

    return this.ReturnResult(withdrawal);
  }

  [Authorize, HttpPost("{userId}/{id:guid}/cancel")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Cancel(
    Guid id,
    string userId,
    [FromBody] CancelWithdrawalReq req
  )
  {
    var withdrawal = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ =>
        cancelWithdrawalReqValidator
          .ValidateAsyncResult(req, "Invalid CancelWithdrawalReq")
          .ThenAwait(r => service.Cancel(id, userId, r.Note))
          .Then(x => x.ToRes(), Errors.MapNone)
      )
      .ThenAwait(enrich.Enrich);

    return this.ReturnResult(withdrawal);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("{id:guid}/reject")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Reject(
    Guid id,
    [FromBody] RejectWithdrawalReq req
  )
  {
    var userId = this.Sub();
    var withdrawal = await rejectWithdrawalReqValidator
      .ValidateAsyncResult(req, "Invalid RejectWithdrawalReq")
      .ThenAwait(x => service.Reject(id, userId!, req.Note))
      .Then(x => x.ToRes(), Errors.MapNone)
      .ThenAwait(enrich.Enrich);

    return this.ReturnResult(withdrawal);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin)]
  [HttpPost("{id:guid}/complete")]
  [Consumes(MediaTypeNames.Multipart.FormData)]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Complete(Guid id, IFormFile file)
  {
    var userId = this.Sub();
    using var stream = new MemoryStream();

    await file.CopyToAsync(stream);
    var x = await service
      .Complete(id, userId!, "", stream)
      .Then(x => x.ToRes(), Errors.MapNone)
      .ThenAwait(enrich.Enrich);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("{id:guid}")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Delete(Guid id)
  {
    var user = await service.Delete(id);
    return this.ReturnUnitNullableResult(
      user,
      new EntityNotFound("User Not Found", typeof(WithdrawalPrincipal), id.ToString())
    );
  }
}
