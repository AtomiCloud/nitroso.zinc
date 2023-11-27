using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Passenger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Passengers.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class PassengerController(
  IPassengerService service,
  CreatePassengerReqValidator createPassengerReqValidator,
  UpdatePassengerReqValidator updatePassengerReqValidator,
  PassengerSearchQueryValidator passengerSearchQueryValidator,
  AuthHelper authHelper
) : AtomiControllerBase(authHelper)
{
  [Authorize, HttpGet]
  public async Task<ActionResult<IEnumerable<PassengerPrincipalRes>>> Search([FromQuery] SearchPassengerQuery query)
  {
    var x = await this
      .GuardOrAnyAsync(query.UserId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => passengerSearchQueryValidator.ValidateAsyncResult(query, "Invalid SearchPassengerQuery"))
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  [Authorize, HttpGet("{userId}/{id:guid}")]
  public async Task<ActionResult<PassengerRes>> Get(string userId, Guid id)
  {
    var r = await this
      .GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.Get(userId, id))
      .Then(x => x.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(r, new EntityNotFound(
      "Passenger Not Found", typeof(Passenger), id.ToString()));
  }

  [Authorize, HttpPost("{userId}")]
  public async Task<ActionResult<PassengerPrincipalRes>> Create(string userId, [FromBody] CreatePassengerReq req)
  {
    var user = await this.GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => createPassengerReqValidator.ValidateAsyncResult(req, "Invalid CreatePassengerReq"))
      .ThenAwait(x => service.Create(userId, x.ToRecord()))
      .Then(x => x.ToRes(), Errors.MapAll);
    return this.ReturnResult(user);
  }

  [Authorize, HttpPut("{userId}/{id:guid}")]
  public async Task<ActionResult<PassengerPrincipalRes>> Update(string userId, Guid id,
    [FromBody] UpdatePassengerReq req)
  {
    var user = await this.GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => updatePassengerReqValidator.ValidateAsyncResult(req, "Invalid UpdatePassengerReq"))
      .ThenAwait(x => service.Update(userId, id, x.ToRecord()))
      .Then(x => x.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(user, new EntityNotFound(
      "Passenger Not Found", typeof(PassengerPrincipal), id.ToString()));
  }

  [Authorize, HttpDelete("{userId}/{id:guid}")]
  public async Task<ActionResult> Delete(string userId, Guid id)
  {
    var user = await this
      .GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.Delete(userId, id));

    return this.ReturnUnitNullableResult(user, new EntityNotFound(
      "Passenger Not Found", typeof(PassengerPrincipal), id.ToString()));
  }
}
