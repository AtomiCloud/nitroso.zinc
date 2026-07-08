using System.Net.Mime;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using Asp.Versioning;
using CSharp_Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Announcements.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class AnnouncementController(IAnnouncementService service, IAuthHelper h)
  : AtomiControllerBase(h)
{
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("withdrawal-fee/{userId}")]
  public async Task<ActionResult<AnnouncementSentRes>> SendWithdrawalFee(string userId)
  {
    var x = await service
      .SendWithdrawalFeeAnnouncement(userId)
      .Then(u => u.ToSentRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("withdrawal-fee")]
  public async Task<ActionResult<AnnouncementBroadcastRes>> BroadcastWithdrawalFee()
  {
    var x = await service
      .BroadcastWithdrawalFeeAnnouncement()
      .Then(r => r.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }
}
