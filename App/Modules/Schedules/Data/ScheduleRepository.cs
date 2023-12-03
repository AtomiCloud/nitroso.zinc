using App.Error.V1;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Schedule;
using EFCore.BulkExtensions;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Schedules.Data;

public class ScheduleRepository(MainDbContext db, ILogger<ScheduleRepository> logger) : IScheduleRepository
{
  public async Task<Result<DateOnly?>> Latest()
  {
    try
    {
      logger.LogInformation("Getting latest schedule date in database");
      var a = await db.Schedules
        .OrderByDescending(x => x.Date)
        .FirstOrDefaultAsync();
      return a?.Date;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to get latest ScheduleDate");
      return e;
    }
  }

  public async Task<Result<IEnumerable<SchedulePrincipal>>> Range(DateOnly start, DateOnly end)
  {
    try
    {
      logger.LogInformation("Getting schedules from {@StartDate} to {@EndDate}", start, end);
      var a = await db.Schedules
        .Where(x => x.Date >= start && x.Date <= end)
        .ToArrayAsync();
      return a.Select(x => x.ToPrincipal()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed getting schedules from {@StartDate} to {@EndDate}", start, end);
      return e;
    }
  }

  public async Task<Result<Schedule?>> Get(DateOnly date)
  {
    try
    {
      logger.LogInformation("Retrieving Schedule on '{@Date}'", date);
      var user = await db
        .Schedules
        .Where(x => x.Date == date)
        .FirstOrDefaultAsync();
      return user?.ToDomain();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed retrieving Schedule on '{@Date}'", date);
      return e;
    }
  }

  public async Task<Result<SchedulePrincipal>> Update(DateOnly date, ScheduleRecord record)
  {
    try
    {
      logger.LogInformation("Updating Schedule on '{@Date}' with: {@Record}", date, record.ToJson());
      var v1 = await db.Schedules
        .Where(x => x.Date == date)
        .FirstOrDefaultAsync();

      if (v1 == null)
      {
        logger.LogInformation("Schedule on '{@Date}' does not exist. Create with: {@Record}", date, record.ToJson());
        var data = new ScheduleData { Date = date };
        data.UpdateData(record);
        var added = db.Schedules.Add(data);
        await db.SaveChangesAsync();
        return added.Entity.ToPrincipal();
      }

      var v3 = v1.UpdateData(record);
      var updated = db.Schedules.Update(v3);
      await db.SaveChangesAsync();
      return updated.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e,
        "Failed updating Schedule on '{@Date}' with: {@Record}", date, record.ToJson());
      return new EntityConflict(
          $"Failed updating Schedule on '{date}' with: {record.ToJson()} due to conflicting with existing record",
          typeof(SchedulePrincipal))
        .ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed updating Schedule on '{@Date}' with: {@Record}", date, record.ToJson());
      return e;
    }
  }


  public async Task<Result<Unit>> BulkUpdate(IEnumerable<SchedulePrincipal> record)
  {
    try
    {
      logger.LogInformation("Bulk updating Schedule, {@Records}", record.ToJson());
      await db.BulkInsertOrUpdateAsync(record.Select(r => r.ToData()));
      await db.BulkSaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed bulk updating Schedule");
      return e;
    }
  }

  public async Task<Result<Unit?>> Delete(DateOnly date)
  {
    try
    {
      logger.LogInformation("Deleting Schedule on '{@Date}'", date);
      var v1 = await db.Schedules
        .Where(x => x.Date == date)
        .FirstOrDefaultAsync();

      if (v1 == null)
      {
        logger.LogInformation("Schedule on '{@Date}' does not exist.", date);
        return (Unit?)null;
      }

      db.Schedules.Remove(v1);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed deleting Schedule on '{@Date}'", date);
      return e;
    }
  }
}
