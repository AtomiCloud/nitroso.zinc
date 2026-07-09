namespace App.Modules.Milestones.API.V1;

// REQ
// Record a snatching-algorithm change: Date is the dd-MM-yyyy day it took
// effect, Label a short human description (<= 256 chars)
public record CreateMilestoneReq(string Date, string Label);

// RESP
public record MilestoneRes(Guid Id, string Date, string Label, DateTime CreatedAt);
