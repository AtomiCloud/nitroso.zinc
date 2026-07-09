using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Booking;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

public class PrioritySettingsRepository(
  MainDbContext db,
  ILogger<PrioritySettingsRepository> logger
) : IPrioritySettingsRepository
{
  public async Task<Result<PrioritySettingsPrincipal?>> GetCurrent()
  {
    try
    {
      var r = await db
        .PrioritySettings.OrderByDescending(x => x.CreatedAt)
        .ThenByDescending(x => x.Id)
        .FirstOrDefaultAsync();
      return r?.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to read the current priority settings");
      return e;
    }
  }

  public async Task<Result<PrioritySettingsPrincipal>> Create(PrioritySettingsRecord record)
  {
    try
    {
      logger.LogInformation("Creating PrioritySettings: {@Record}", record.ToJson());
      var data = new PrioritySettingsData { CreatedAt = DateTime.UtcNow };
      data.UpdateData(record);
      var r = db.PrioritySettings.Add(data);
      await db.SaveChangesAsync();
      return r.Entity.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create PrioritySettings {@Record}", record.ToJson());
      return e;
    }
  }
}

public class PriorityAccessRepository(MainDbContext db, ILogger<PriorityAccessRepository> logger)
  : IPriorityAccessRepository
{
  public async Task<Result<IEnumerable<PriorityAccess>>> List()
  {
    try
    {
      logger.LogInformation("Listing priority access users");
      var r = await db
        .PriorityAccesses.OrderBy(x => x.CreatedAt)
        .ThenBy(x => x.UserId)
        .ToArrayAsync();
      return r.Select(x => x.ToDomain()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed listing priority access users");
      return e;
    }
  }

  public async Task<Result<PriorityAccess>> Add(string userId)
  {
    try
    {
      logger.LogInformation("Adding priority access for User '{UserId}'", userId);
      var existing = await db
        .PriorityAccesses.Where(x => x.UserId == userId)
        .FirstOrDefaultAsync();
      // idempotent: allowlisting twice is a no-op
      if (existing != null)
        return existing.ToDomain();
      var data = new PriorityAccessData { UserId = userId, CreatedAt = DateTime.UtcNow };
      var r = db.PriorityAccesses.Add(data);
      await db.SaveChangesAsync();
      return r.Entity.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to add priority access for User '{UserId}'", userId);
      return e;
    }
  }

  public async Task<Result<Unit?>> Remove(string userId)
  {
    try
    {
      logger.LogInformation("Removing priority access for User '{UserId}'", userId);
      var data = await db.PriorityAccesses.Where(x => x.UserId == userId).FirstOrDefaultAsync();
      if (data == null)
        return (Unit?)null;
      db.PriorityAccesses.Remove(data);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to remove priority access for User '{UserId}'", userId);
      return e;
    }
  }

  public async Task<Result<bool>> Contains(string userId)
  {
    try
    {
      return await db.PriorityAccesses.AnyAsync(x => x.UserId == userId);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to check priority access for User '{UserId}'", userId);
      return e;
    }
  }
}

public static class PriorityDataMapper
{
  // Data -> Domain
  public static PrioritySettingsRecord ToRecord(this PrioritySettingsData data) =>
    new()
    {
      Fee = data.Fee,
      AllowAll = data.AllowAll,
      WindowStartSgt = data.WindowStartSgt,
      WindowEndSgt = data.WindowEndSgt,
    };

  public static PrioritySettingsPrincipal ToPrincipal(this PrioritySettingsData data) =>
    new()
    {
      Id = data.Id,
      CreatedAt = data.CreatedAt,
      Record = data.ToRecord(),
    };

  public static PriorityAccess ToDomain(this PriorityAccessData data) =>
    new() { UserId = data.UserId, CreatedAt = data.CreatedAt };

  // Domain -> Data
  public static PrioritySettingsData UpdateData(
    this PrioritySettingsData data,
    PrioritySettingsRecord record
  )
  {
    data.Fee = record.Fee;
    data.AllowAll = record.AllowAll;
    data.WindowStartSgt = record.WindowStartSgt;
    data.WindowEndSgt = record.WindowEndSgt;
    return data;
  }
}
