namespace App.Modules.Announcements.API.V1;

// RESP
public record AnnouncementSentRes(string UserId, string Email);

public record AnnouncementBroadcastRes(int Sent, int Failed, string[] FailedUserIds);
