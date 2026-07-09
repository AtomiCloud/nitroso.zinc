using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Milestone;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Milestones.Data;

public class MilestoneRepository(MainDbContext db, ILogger<MilestoneRepository> logger)
  : IMilestoneRepository
{
  public async Task<Result<IEnumerable<MilestonePrincipal>>> List()
  {
    try
    {
      // newest Date first; CreatedAt then Id break ties deterministically
      var milestones = await db
        .Milestones.OrderByDescending(x => x.Date)
        .ThenByDescending(x => x.CreatedAt)
        .ThenByDescending(x => x.Id)
        .ToArrayAsync();
      return milestones.Select(x => x.ToPrincipal()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to list Milestones");
      return e;
    }
  }

  public async Task<Result<MilestonePrincipal>> Add(MilestoneRecord record)
  {
    try
    {
      logger.LogInformation("Creating Milestone: {@Record}", record.ToJson());
      var data = new MilestoneData
      {
        CreatedAt = DateTime.UtcNow,
        Date = record.Date,
        Label = record.Label,
      };
      db.Milestones.Add(data);
      await db.SaveChangesAsync();
      return data.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create Milestone: {@Record}", record.ToJson());
      return e;
    }
  }

  public async Task<Result<MilestonePrincipal?>> Delete(Guid id)
  {
    try
    {
      var milestone = await db.Milestones.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (milestone == null)
        return (MilestonePrincipal?)null;

      logger.LogInformation("Deleting Milestone '{Id}'", id);
      db.Milestones.Remove(milestone);
      await db.SaveChangesAsync();
      return milestone.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete Milestone '{Id}'", id);
      return e;
    }
  }
}

public static class MilestoneDataMapper
{
  public static MilestoneRecord ToRecord(this MilestoneData data) =>
    new() { Date = data.Date, Label = data.Label };

  public static MilestonePrincipal ToPrincipal(this MilestoneData data) =>
    new()
    {
      Id = data.Id,
      CreatedAt = data.CreatedAt,
      Record = data.ToRecord(),
    };
}
