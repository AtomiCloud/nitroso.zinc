namespace App.Modules.Discounts.API.V1;

public record DiscountSearchQuery(
  string? Search,
  string? DiscountType,
  string? MatchMode,
  string[]? MatchTarget,
  bool? Disabled
);

public record DiscountMatchReq(string Value, string MatchType);

public record DiscountTargetReq(string MatchMode, DiscountMatchReq[] Matches);

public record DiscountRecordReq(string Name, string Description, decimal Amount, string Type);

public record DiscountStatusReq(bool Disabled);

public record CreateDiscountReq(DiscountTargetReq Target, DiscountRecordReq Record);

public record UpdateDiscountReq(
  DiscountTargetReq Target,
  DiscountRecordReq Record,
  DiscountStatusReq Status
);

// RESP
public record DiscountMatchRes(string Value, string MatchType);

public record DiscountTargetRes(string MatchMode, DiscountMatchRes[] Matches);

public record DiscountRecordRes(string Name, string Description, decimal Amount, string Type);

public record DiscountStatusRes(bool Disabled);

public record DiscountPrincipalRes(
  Guid Id,
  DiscountRecordRes Record,
  DiscountStatusRes Status,
  DiscountTargetRes Target
);
