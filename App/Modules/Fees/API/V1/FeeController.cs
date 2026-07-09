using System.Net.Mime;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Fees.API.V1;

// The fee queue: per fee type (Withdrawal | Deposit), an admin can queue
// "on this date change to X" events (flat + percentage), see the queue, and
// cancel events that have not yet taken effect. With no effective event the
// fee is zero-zero.
[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class FeeController(
  IFeeCalculator feeCalculator,
  IFeeRepository feeRepository,
  AddFeeReqValidator addFeeReqValidator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  // the fee in effect right now, for pre-submission display
  [Authorize, HttpGet("{type}")]
  public async Task<ActionResult<FeeSpecRes>> Current(FeeType type)
  {
    var x = await feeCalculator.Current(type).Then(s => s.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // the queue: scheduled future fee changes, soonest first
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("{type}/upcoming")]
  public async Task<ActionResult<IEnumerable<FeeEventRes>>> Upcoming(FeeType type)
  {
    var x = await feeRepository
      .GetUpcoming(type)
      .Then(cs => cs.Select(c => c.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // queue a fee change (immediate when EffectiveAt is null)
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("{type}")]
  public async Task<ActionResult<FeeEventRes>> Add(FeeType type, [FromBody] AddFeeReq req)
  {
    var x = await addFeeReqValidator
      .ValidateAsyncResult(req, "Invalid AddFeeReq")
      .ThenAwait(r => feeRepository.Add(type, r.Percentage, r.FlatAmount, r.EffectiveAt))
      .Then(c => c.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // cancel a QUEUED change — only events that have not yet taken effect can
  // be removed; effective history is immutable
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("{id:guid}")]
  public async Task<ActionResult<FeeEventRes>> Cancel(Guid id)
  {
    var x = await feeRepository
      .CancelUpcoming(id)
      .NullToError(id.ToString())
      .Then(c => c.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }
}
