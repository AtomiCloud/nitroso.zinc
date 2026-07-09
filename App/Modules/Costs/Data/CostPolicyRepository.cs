using App.Modules.Timings.Data;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Cost;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Costs.Data;

public class CostPolicyRepository(MainDbContext db, ILogger<CostPolicyRepository> logger)
  : ICostPolicyRepository
{
  public async Task<Result<IEnumerable<CostPolicyPrincipal>>> List()
  {
    try
    {
      logger.LogInformation("Listing cost policies");
      var r = await db.CostPolicies.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync();
      return r.Select(x => x.ToPrincipal()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed listing cost policies");
      return e;
    }
  }

  public async Task<Result<CostPolicyPrincipal>> Create(CostPolicyRecord record)
  {
    try
    {
      logger.LogInformation("Creating CostPolicy: {@Record}", record.ToJson());
      var data = new CostPolicyData { CreatedAt = DateTime.UtcNow };
      data.UpdateData(record);
      var r = db.CostPolicies.Add(data);
      await db.SaveChangesAsync();
      return r.Entity.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create CostPolicy {@Record}", record.ToJson());
      return e;
    }
  }

  public async Task<Result<CostPolicyPrincipal?>> Update(Guid id, CostPolicyRecord record)
  {
    try
    {
      logger.LogInformation("Updating CostPolicy '{Id}' with: {@Record}", id, record.ToJson());
      var data = await db.CostPolicies.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (data == null)
        return (CostPolicyPrincipal?)null;
      data.UpdateData(record);
      await db.SaveChangesAsync();
      return data.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to update CostPolicy '{Id}' with {@Record}", id, record.ToJson());
      return e;
    }
  }

  public async Task<Result<Unit?>> Delete(Guid id)
  {
    try
    {
      logger.LogInformation("Deleting CostPolicy '{Id}'", id);
      var data = await db.CostPolicies.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (data == null)
        return (Unit?)null;
      db.CostPolicies.Remove(data);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete CostPolicy '{Id}'", id);
      return e;
    }
  }
}

public static class CostPolicyDataMapper
{
  // Data -> Domain
  public static CostPolicyRecord ToRecord(this CostPolicyData data) =>
    new()
    {
      Name = data.Name,
      Enabled = data.Enabled,
      MatchDate = data.MatchDate,
      MatchTime = data.MatchTime,
      MatchDayOfWeek = data.MatchDayOfWeek == null ? null : (DayOfWeek)data.MatchDayOfWeek.Value,
      MatchDirection = data.MatchDirection?.ToTrainDirection(),
      LeadTimeUnderHours = data.LeadTimeUnderHours,
      Amount = data.Amount,
      IsPercentage = data.IsPercentage,
      EffectiveAt = data.EffectiveAt,
      ExpiresAt = data.ExpiresAt,
    };

  public static CostPolicyPrincipal ToPrincipal(this CostPolicyData data) =>
    new()
    {
      Id = data.Id,
      CreatedAt = data.CreatedAt,
      Record = data.ToRecord(),
    };

  // Domain -> Data
  public static CostPolicyData UpdateData(this CostPolicyData data, CostPolicyRecord record)
  {
    data.Name = record.Name;
    data.Enabled = record.Enabled;
    data.MatchDate = record.MatchDate;
    data.MatchTime = record.MatchTime;
    data.MatchDayOfWeek = record.MatchDayOfWeek == null ? null : (byte)record.MatchDayOfWeek.Value;
    data.MatchDirection = record.MatchDirection?.ToData();
    data.LeadTimeUnderHours = record.LeadTimeUnderHours;
    data.Amount = record.Amount;
    data.IsPercentage = record.IsPercentage;
    // normalize to UTC: JSON without a Z suffix binds as Unspecified and an
    // offset binds as Local — Npgsql rejects both for timestamptz
    data.EffectiveAt = record.EffectiveAt.ToUtcOrNull();
    data.ExpiresAt = record.ExpiresAt.ToUtcOrNull();
    return data;
  }

  private static DateTime? ToUtcOrNull(this DateTime? at) =>
    at?.Kind switch
    {
      null => null,
      DateTimeKind.Utc => at.Value,
      DateTimeKind.Local => at.Value.ToUniversalTime(),
      _ => DateTime.SpecifyKind(at.Value, DateTimeKind.Utc),
    };
}
