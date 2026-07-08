using Domain.User;

namespace App.Modules.Announcements.API.V1;

public static class AnnouncementMapper
{
  public static AnnouncementSentRes ToSentRes(this UserPrincipal user) =>
    new(user.Id, user.Record.Email ?? string.Empty);

  public static AnnouncementBroadcastRes ToRes(this AnnouncementBroadcastResult result) =>
    new(result.Sent, result.Failed, result.FailedUserIds);
}
