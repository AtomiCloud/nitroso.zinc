namespace App.Modules.Announcements.API.V1;

// REQ
// Type: "Withdrawal" | "Deposit". ChangeId targets a specific queued fee
// change (defaults to the next upcoming one, else the live fee). Reasoning
// is the admin's customizable explanation of why the fee is changing.
public record SendFeeAnnouncementReq(string Type, Guid? ChangeId, string? Reasoning);

// RESP
public record AnnouncementSentRes(string UserId, string Email);

public record AnnouncementBroadcastRes(int Sent, int Failed, string[] FailedUserIds);
