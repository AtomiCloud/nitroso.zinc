using App.StartUp.Database;
using CSharp_Result;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Withdrawals.Data;

public class FeeRepository(MainDbContext db, ILogger<FeeRepository> logger) : IFeeRepository
{
  public async Task<Result<FeeChange?>> GetCurrent(FeeType type)
  {
    try
    {
      var now = DateTime.UtcNow;
      var latest = await db
        .Fees.Where(x => x.Type == (byte)type && x.EffectiveAt <= now)
        .OrderByDescending(x => x.EffectiveAt)
        .ThenByDescending(x => x.CreatedAt)
        .ThenByDescending(x => x.Id)
        .FirstOrDefaultAsync();
      return latest?.ToChange();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to read the current {Type} fee", type);
      return e;
    }
  }

  public async Task<Result<IEnumerable<FeeChange>>> GetUpcoming(FeeType type)
  {
    try
    {
      var now = DateTime.UtcNow;
      var upcoming = await db
        .Fees.Where(x => x.Type == (byte)type && x.EffectiveAt > now)
        .OrderBy(x => x.EffectiveAt)
        .ToArrayAsync();
      return upcoming.Select(x => x.ToChange()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to read upcoming {Type} fee changes", type);
      return e;
    }
  }

  public async Task<Result<FeeChange>> Add(
    FeeType type,
    decimal percentage,
    decimal flatAmount,
    DateTime? effectiveAt
  )
  {
    try
    {
      var now = DateTime.UtcNow;
      logger.LogInformation(
        "Queueing {Type} fee change: {Percentage}% + {Flat} flat, effective {EffectiveAt}",
        type,
        percentage,
        flatAmount,
        effectiveAt ?? now
      );
      // normalize to UTC: JSON without a Z suffix binds as Unspecified and
      // an offset binds as Local — Npgsql rejects both for timestamptz
      var effective = effectiveAt?.Kind switch
      {
        null => now,
        DateTimeKind.Utc => effectiveAt.Value,
        DateTimeKind.Local => effectiveAt.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(effectiveAt.Value, DateTimeKind.Utc),
      };
      var data = new FeeData
      {
        CreatedAt = now,
        EffectiveAt = effective,
        Type = (byte)type,
        Percentage = percentage,
        FlatAmount = flatAmount,
      };
      db.Fees.Add(data);
      await db.SaveChangesAsync();
      return data.ToChange();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to queue {Type} fee change", type);
      return e;
    }
  }

  public async Task<Result<FeeChange?>> CancelUpcoming(Guid id)
  {
    try
    {
      var now = DateTime.UtcNow;
      var row = await db
        .Fees.Where(x => x.Id == id && x.EffectiveAt > now)
        .FirstOrDefaultAsync();
      if (row == null)
        return (FeeChange?)null;
      logger.LogInformation("Cancelling queued fee change '{Id}'", id);
      db.Fees.Remove(row);
      await db.SaveChangesAsync();
      return row.ToChange();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to cancel queued fee change '{Id}'", id);
      return e;
    }
  }
}

public static class FeeMapper
{
  public static FeeChange ToChange(this FeeData data) =>
    new()
    {
      Id = data.Id,
      Type = (FeeType)data.Type,
      Percentage = data.Percentage,
      FlatAmount = data.FlatAmount,
      EffectiveAt = data.EffectiveAt,
    };
}
