using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Transaction;
using Domain.Wallet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Transactions.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class TransactionController(
  ITransactionService service,
  TransactionSearchQueryValidator transactionSearchQueryValidator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  [Authorize, HttpGet]
  public async Task<ActionResult<IEnumerable<TransactionPrincipalRes>>> Search(
    [FromQuery] SearchTransactionQuery query
  )
  {
    var x = await this.GuardOrAnyAsync(query.userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ =>
        transactionSearchQueryValidator.ValidateAsyncResult(query, "Invalid TransactionSearchQuery")
      )
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize, HttpGet("{id:guid}")]
  public async Task<ActionResult<TransactionRes>> Get(Guid id, string? userId)
  {
    var wallet = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.Get(id, userId))
      .Then(x => x?.ToRes(), Errors.MapAll);

    return this.ReturnNullableResult(
      wallet,
      new EntityNotFound("Transaction Not Found", typeof(Transaction), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("{id:guid}")]
  public async Task<ActionResult<TransactionRes>> Delete(Guid id)
  {
    var r = await service.Delete(id);

    return this.ReturnUnitNullableResult(
      r,
      new EntityNotFound("Transaction Not Found", typeof(Transaction), id.ToString())
    );
  }
}
