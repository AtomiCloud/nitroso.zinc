using App.Modules.Discounts.API.V1;

namespace App.Modules.Costs.API.V1;

public record CreateCostReq(decimal Cost);

// REQ
// A pricing rule. Match* null = matches any value for that dimension.
// MatchDate: dd-MM-yyyy, MatchTime: HH:mm:ss, MatchDayOfWeek: Monday..Sunday,
// MatchDirection: JToW | WToJ. Amount is SIGNED (negative = discount) and a
// percent of the base cost when IsPercentage.
public record CostPolicyReq(
  string Name,
  bool Enabled,
  string? MatchDate,
  string? MatchTime,
  string? MatchDayOfWeek,
  string? MatchDirection,
  int? LeadTimeUnderHours,
  decimal Amount,
  bool IsPercentage,
  DateTime? EffectiveAt,
  DateTime? ExpiresAt
);

// query for the price breakdown of a hypothetical booking spec
public record CostSummaryQuery(string Date, string Time, string Direction);

// RESP
public record CostPrincipalRes(Guid Id, DateTime CreatedAt, decimal Cost);

public record CostPolicyLineRes(string Name, decimal Delta);

public record MaterializedCostRes(
  decimal Cost,
  CostPolicyLineRes[] PolicyLines,
  decimal Subtotal,
  decimal Final,
  DiscountRecordRes[] Discounts
);

// the full price breakdown for one booking spec — every step adds up:
// BaseCost + sum(PolicyLines.Delta) = Subtotal (floored at 0), Subtotal
// less Discounts = Final
public record CostSummaryRes(
  decimal BaseCost,
  CostPolicyLineRes[] PolicyLines,
  decimal Subtotal,
  DiscountRecordRes[] Discounts,
  decimal Final
);

public record CostPolicyPrincipalRes(
  Guid Id,
  DateTime CreatedAt,
  string Name,
  bool Enabled,
  string? MatchDate,
  string? MatchTime,
  string? MatchDayOfWeek,
  string? MatchDirection,
  int? LeadTimeUnderHours,
  decimal Amount,
  bool IsPercentage,
  DateTime? EffectiveAt,
  DateTime? ExpiresAt
);
