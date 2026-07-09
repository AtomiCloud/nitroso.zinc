using CSharp_Result;
using Domain;
using Domain.User;

namespace App.Modules.Announcements;

public record AnnouncementBroadcastResult
{
  public required int Sent { get; init; }

  public required int Failed { get; init; }

  public required string[] FailedUserIds { get; init; }
}

// What to announce: a specific queued fee change (ChangeId), else the next
// upcoming change of the type, else the live fee as an immediate change.
// Reasoning is the admin's own explanation of WHY the fee is changing; when
// omitted the default anti-abuse copy is used.
public record FeeAnnouncementSpec
{
  public required FeeType Type { get; init; }

  public Guid? ChangeId { get; init; }

  public string? Reasoning { get; init; }
}

public interface IAnnouncementService
{
  Task<Result<UserPrincipal>> SendFeeAnnouncement(string userId, FeeAnnouncementSpec spec);

  Task<Result<AnnouncementBroadcastResult>> BroadcastFeeAnnouncement(FeeAnnouncementSpec spec);
}
