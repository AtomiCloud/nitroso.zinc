using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Bookings.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class BookingController(
  ITrainBookingService service,
  CreateBookingReqValidator createBookingReqValidator,
  BookingSearchQueryValidator bookingSearchQueryValidator,
  AuthHelper authHelper
) : AtomiControllerBase(authHelper)
{
  [Authorize, HttpGet]
  public async Task<ActionResult<IEnumerable<BookingPrincipalRes>>> Search([FromQuery] SearchBookingQuery query)
  {
    var x = await this
      .GuardOrAnyAsync(query.UserId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => bookingSearchQueryValidator.ValidateAsyncResult(query, "Invalid SearchBookingQuery"))
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  [Authorize, HttpGet("{userId}/{id:guid}")]
  public async Task<ActionResult<BookingRes>> Get(string userId, Guid id)
  {
    var x = await this
      .GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.Get(userId, id))
      .Then(x => x?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(x, new EntityNotFound("Booking not found", typeof(Booking), id.ToString()));
  }

  [Authorize(Policy = AuthPolicies.AdminOrBuyer), HttpPost("complete/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Complete(Guid id)
  {
    var x = await service.Complete(id)
      .Then(x => x?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(x, new EntityNotFound("Booking not found", typeof(Booking), id.ToString()));
  }

  [Authorize(Policy = AuthPolicies.AdminOrCountSyncer), HttpPost("counts")]
  public async Task<ActionResult<IEnumerable<BookingCountRes>>> CountStatus()
  {
    var x = await service.Count()
      .Then(x => x.Select(x => x.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }





  //TODO: CANCEL
  //TODO: CREATE



}
