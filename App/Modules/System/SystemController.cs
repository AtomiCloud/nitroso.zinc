using App.Modules.Common;
using App.StartUp.Options;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace App.Modules.System;

[ApiVersionNeutral]
[ApiController]
[Route("/")]
public class SystemController(IOptionsSnapshot<AppOption> app) : AtomiControllerBase
{
  [HttpGet]
  public ActionResult<object> SystemInfo()
  {
    var v = app.Value;
    return this.Ok(new
    {
      v.Landscape,
      v.Platform,
      v.Service,
      v.Module,
      v.Version,
      Status = "OK",
      TimeStamp = DateTime.UtcNow,
    });
  }
}
