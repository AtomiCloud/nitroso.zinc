using Domain;
using Domain.User;

namespace App.Modules.Announcements.API.V1;

public static class AnnouncementMapper
{
  public static FeeAnnouncementSpec ToSpec(this SendFeeAnnouncementReq req) =>
    new()
    {
      Type = Enum.Parse<FeeType>(req.Type, true),
      ChangeId = req.ChangeId,
      Reasoning = req.Reasoning,
    };

  public static AnnouncementSentRes ToSentRes(this UserPrincipal user) =>
    new(user.Id, user.Record.Email ?? string.Empty);

  public static AnnouncementBroadcastRes ToRes(this AnnouncementBroadcastResult result) =>
    new(result.Sent, result.Failed, result.FailedUserIds);
}
