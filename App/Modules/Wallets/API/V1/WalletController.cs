using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.Modules.Users.API.V1;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.User;
using Domain.Wallet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Wallets.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class WalletController(
  IWalletService service,
  WalletSearchQueryValidator walletSearchQueryValidator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet]
  public async Task<ActionResult<IEnumerable<WalletPrincipalRes>>> Search([FromQuery] SearchWalletQuery query)
  {
    var x = await walletSearchQueryValidator
      .ValidateAsyncResult(query, "Invalid WalletSearchQuery")
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize, HttpGet("{id:guid}")]
  public async Task<ActionResult<WalletRes>> Get(Guid id, string? userId)
  {
    var wallet = await
      this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
        .ThenAwait(_ => service.Get(id, userId))
        .Then(x => x?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(wallet, new EntityNotFound(
      "Wallet Not Found", typeof(Wallet), id.ToString()));
  }

  [Authorize, HttpGet("user/{userId}")]
  public async Task<ActionResult<WalletRes>> GetByUserId(string userId)
  {
    var wallet = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.GetByUserId(userId))
      .Then(x => x?.ToRes(), Errors.MapNone);
    return this.ReturnNullableResult(wallet, new EntityNotFound(
      "Wallet Not Found", typeof(Wallet), userId));
  }
}
