using App.Error.V1;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Cost;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Costs.Data;

public class CostRepository(MainDbContext db, ILogger<CostRepository> logger) : ICostRepository
{
  public async Task<Result<IEnumerable<CostPrincipal>>> History()
  {
    try
    {
      logger.LogInformation("Getting cost history");
      return await db.Costs.OrderByDescending(x => x.CreatedAt)
        .Select(x => x.ToPrincipal())
        .ToArrayAsync();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed getting cost history");
      return e;
    }
  }

  public async Task<Result<CostPrincipal>> Create(CostRecord record)
  {
    try
    {
      logger.LogInformation("Creating Cost: {@Record}", record.ToJson());
      var data = new CostData { CreatedAt = DateTime.UtcNow };
      data.UpdateData(record);
      var r = db.Costs.Add(data);
      await db.SaveChangesAsync();
      return r.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e,
        "Failed to create Cost {@Record} due to conflict with existing record",
        record.ToJson());
      return new EntityConflict(
          "Failed to create Cost  due to conflicting with existing record",
          typeof(CostPrincipal))
        .ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create Cost {@Record}",
        record.ToJson());
      return e;
    }
  }

  public async Task<Result<CostPrincipal?>> GetCurrent()
  {
    var r = await db.Costs.OrderByDescending(x => x.CreatedAt)
      .FirstOrDefaultAsync();
    return r?.ToPrincipal();
  }
}
