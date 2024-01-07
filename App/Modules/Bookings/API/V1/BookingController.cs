using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.Modules.Timings.API.V1;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Booking;
using Domain.Cost;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Utils = App.Utility.Utils;

namespace App.Modules.Bookings.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class BookingController(
  IBookingService service,
  ICostCalculator costCalculator,
  CreateBookingReqValidator createBookingReqValidator,
  BookingSearchQueryValidator bookingSearchQueryValidator,
  ReserveBookingQueryValidator reserveBookingQueryValidator,
  IAuthHelper authHelper,
  ILogger<BookingController> logger,
  IBookingImageEnricher enrich
) : AtomiControllerBase(authHelper)
{
  [Authorize, HttpGet]
  public async Task<ActionResult<IEnumerable<BookingPrincipalRes>>> Search([FromQuery] SearchBookingQuery query)
  {
    var x = await this
      .GuardOrAnyAsync(query.UserId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => bookingSearchQueryValidator.ValidateAsyncResult(query, "Invalid SearchBookingQuery"))
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapAll)
      .ThenAwait(x => enrich.Enrich(x));

    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpGet("reserve/{Direction}/{Date}/{Time}")]
  public async Task<ActionResult<BookingPrincipalRes>> Reserve([FromRoute] ReserveBookingQuery query)
  {
    var book = await reserveBookingQueryValidator.ValidateAsyncResult(query, "Invalid ReserveBookingQuery")
      .ThenAwait(q => service.Reserve(
        q.Direction.DirectionToDomain(), q.Date.ToDate(), q.Time.ToTime()))
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(book,
      new EntityNotFound("Booking not found", typeof(Booking), $"{query.Direction}-{query.Date}-{query.Time}"));
  }

  [Authorize, HttpGet("{id:guid}")]
  public async Task<ActionResult<BookingRes>> Get(Guid id, string? userId)
  {
    var x = await this
      .GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.Get(userId, id))
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(x, new EntityNotFound("Booking not found", typeof(Booking), id.ToString()));
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpPost("complete/{id:guid}")]
  [Consumes(MediaTypeNames.Multipart.FormData)]
  public async Task<ActionResult<BookingPrincipalRes>> Complete(Guid id, IFormFile file)
  {
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    logger.LogInformation("Stream Size: {StreamSize}", stream.Length);
    var x = await service.Complete(id, stream)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(x, new EntityNotFound("Booking not found", typeof(Booking), id.ToString()));
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpGet("counts")]
  public async Task<ActionResult<IEnumerable<BookingCountRes>>> CountStatus()
  {
    var x = await service.Count()
      .Then(x => x.Select(c => c.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("buying/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Buying(Guid id)
  {
    var x = await service.Buying(id)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(x, new EntityNotFound("Booking not found", typeof(Booking), id.ToString()));
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("refund/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Refund(Guid id)
  {
    var x = await service.Refund(id)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(x, new EntityNotFound("Booking not found", typeof(Booking), id.ToString()));
  }


  // Purchase
  [Authorize, HttpPost("{userId}/purchase")]
  public async Task<ActionResult<BookingPrincipalRes>> Purchase(string userId, [FromBody] CreateBookingReq req)
  {
    var p = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => createBookingReqValidator.ValidateAsyncResult(req, "Failed to validate CreateBookingReq"))
      .Then(r => r.ToRecord(), Errors.MapNone)
      .ThenAwait(rec => costCalculator
        .BookingCost(userId, rec)
        .Then(cost => (c: cost, r: rec), Errors.MapNone)
      )
      .ThenAwait(cr => service.Create(userId, cr.c, cr.r))
      .Then(b => b.ToRes(), Errors.MapNone);

    return this.ReturnResult(p);
  }

  // cancel
  [HttpPost("cancel/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Cancel(Guid id, string? userId)
  {
    var p = await this
      .GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(cr => service.Cancel(userId, id))
      .Then(b => b?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(p,
      new EntityNotFound("Cannot find booking to be cancelled", typeof(BookingPrincipal), id.ToString()));
  }

  // terminate
  [HttpPost("terminate/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Terminate(Guid id, string? userId)
  {
    var p = await this
      .GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(cr => service.Terminate(userId, id))
      .Then(b => b?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(p,
      new EntityNotFound("Cannot find booking to be terminated", typeof(BookingPrincipal), id.ToString()));
  }
}
