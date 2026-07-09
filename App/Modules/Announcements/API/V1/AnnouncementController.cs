using System.Net.Mime;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Announcements.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class AnnouncementController(
  IAnnouncementService service,
  SendFeeAnnouncementReqValidator validator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  // send to ONE user — for testing the email before a broadcast
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("fee/{userId}")]
  public async Task<ActionResult<AnnouncementSentRes>> SendFee(
    string userId,
    [FromBody] SendFeeAnnouncementReq req
  )
  {
    var x = await validator
      .ValidateAsyncResult(req, "Invalid SendFeeAnnouncementReq")
      .ThenAwait(r => service.SendFeeAnnouncement(userId, r.ToSpec()))
      .Then(u => u.ToSentRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("fee")]
  public async Task<ActionResult<AnnouncementBroadcastRes>> BroadcastFee(
    [FromBody] SendFeeAnnouncementReq req
  )
  {
    var x = await validator
      .ValidateAsyncResult(req, "Invalid SendFeeAnnouncementReq")
      .ThenAwait(r => service.BroadcastFeeAnnouncement(r.ToSpec()))
      .Then(r => r.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }
}
