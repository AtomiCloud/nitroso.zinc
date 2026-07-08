using CSharp_Result;
using Domain.User;

namespace App.Modules.Announcements;

public record AnnouncementBroadcastResult
{
  public required int Sent { get; init; }

  public required int Failed { get; init; }

  public required string[] FailedUserIds { get; init; }
}

public interface IAnnouncementService
{
  Task<Result<UserPrincipal>> SendWithdrawalFeeAnnouncement(string userId);

  Task<Result<AnnouncementBroadcastResult>> BroadcastWithdrawalFeeAnnouncement();
}
