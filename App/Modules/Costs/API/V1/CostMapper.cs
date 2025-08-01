using App.Modules.Discounts.API.V1;
using Domain.Cost;

namespace App.Modules.Costs.API.V1;

public static class CostMapper
{
  // Domain -> RES
  public static CostPrincipalRes ToRes(this CostPrincipal principal) =>
    new(principal.Id, principal.CreatedAt, principal.Record.Cost);

  public static MaterializedCostRes ToRes(this MaterializedCost cost) =>
    new(cost.Cost, cost.Final, cost.Discounts.Select(x => x.ToRes()).ToArray());

  // REQ -> Domain
  public static CostRecord ToDomain(this CreateCostReq req) => new() { Cost = req.Cost };
}
