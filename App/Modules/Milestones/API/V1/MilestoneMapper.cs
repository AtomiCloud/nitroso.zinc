using App.Utility;
using Domain.Milestone;

namespace App.Modules.Milestones.API.V1;

public static class MilestoneApiMapper
{
  // REQ -> DOMAIN
  public static MilestoneRecord ToRecord(this CreateMilestoneReq req) =>
    new() { Date = req.Date.ToDate(), Label = req.Label };

  // DOMAIN -> RES
  public static MilestoneRes ToRes(this MilestonePrincipal p) =>
    new(p.Id, p.Record.Date.ToStandardDateFormat(), p.Record.Label, p.CreatedAt);
}
