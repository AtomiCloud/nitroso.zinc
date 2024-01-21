using App.Modules.Discounts.API.V1;

namespace App.Modules.Costs.API.V1;

public record CreateCostReq(decimal Cost);

// RESP
public record CostPrincipalRes(Guid Id, DateTime CreatedAt, decimal Cost);

public record MaterializedCostRes(
  decimal Cost, decimal Final,
  DiscountRecordRes[] Discounts);
