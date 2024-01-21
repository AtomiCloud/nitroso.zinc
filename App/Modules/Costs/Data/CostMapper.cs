using Domain.Cost;

namespace App.Modules.Costs.Data;

public static class CostMapper
{
  // Data -> Domain
  public static CostRecord ToRecord(this CostData data)
  {
    return new CostRecord { Cost = data.Cost };
  }

  public static CostPrincipal ToPrincipal(this CostData data)
  {
    return new CostPrincipal { Id = data.Id, CreatedAt = data.CreatedAt, Record = data.ToRecord(), };
  }

  // Domain -> Data 
  public static CostData UpdateData(this CostData data, CostRecord record)
  {
    data.Cost = record.Cost;
    return data;
  }
}
