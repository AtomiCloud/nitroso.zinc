using App.StartUp.Database;
using App.StartUp.Registry;
using App.Utility;
using CSharp_Result;
using Domain.Timings;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace App.Modules.Timings.Data;

public class TimingRepository(MainDbContext db, IRedisClientFactory factory, ILogger<TimingRepository> logger)
  : ITimingRepository
{
  public IRedisDatabase Redis => factory.GetRedisClient(Caches.Main).Db0;

  public string RedisKey(TrainDirection direction) => $"timing-train-direction-{direction.ToData()}";

  public async Task<Result<Timing?>> Get(TrainDirection direction)
  {
    try
    {
      logger.LogInformation("Getting timings for {@Direction}", direction);

      var redisKey = this.RedisKey(direction);
      var r = await this.Redis.GetAsync<string[]>(redisKey);
      if (r == null)
      {
        logger.LogInformation("Timings for {@Direction} not found in cache", direction);
        logger.LogInformation("Getting timings for {@Direction} from database", direction);
        var ret = await db.Timings
          .Where(x => x.Direction == direction.ToData())
          .FirstOrDefaultAsync();

        if (ret == null) return (Timing?)null;
        logger.LogInformation("Caching timings for {@Direction}...", direction);

        logger.LogInformation("Standard Timings: {@Timings}", ret.Timings);

        var success = await this.Redis.AddAsync<string[]>(redisKey, ret.Timings.Select(x => x.ToStandardTimeFormat()).ToArray());
        if (!success) logger.LogWarning("Failed to cache timings for {@Direction}", direction);
        else logger.LogInformation("Successfully cached timings for {@Direction}", direction);
        return ret.ToDomain();
      }

      logger.LogInformation("Obtained timings for {@Direction} from cache", direction);
      return new Timing
      {
        Principal = new() { Direction = direction, Record = new() { Timings = r.Select(x => x.ToTime()) } }
      };
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed getting Timings for '{@Direction}'", direction);
      return e;
    }
  }

  public async Task<Result<TimingPrincipal>> Update(TrainDirection direction, TimingRecord record)
  {
    try
    {
      logger.LogInformation("Updating Timing on '{@Direction}' with: {@Record} on Cache", direction, record.ToJson());
      var redisKey = this.RedisKey(direction);
      var success = await this.Redis.AddAsync(redisKey, record.Timings.Select(x => x.ToStandardTimeFormat()));
      if (!success) logger.LogWarning("Failed to cache timings for {@Direction}", direction);

      else logger.LogInformation("Successfully cached timings for {@Direction}", direction);

      logger.LogInformation("Updating Timing on '{@Direction}' with: {@Record} on Database", direction, record.ToJson());

      var v1 = await db.Timings
        .Where(x => x.Direction == direction.ToData())
        .FirstOrDefaultAsync();

      if (v1 == null)
      {
        v1 = new TimingData { Direction = direction.ToData(), Timings = [] };
        var added = db.Timings.Add(v1);
        await db.SaveChangesAsync();
        return added.Entity.ToPrincipal();
      }
      var v3 = v1.UpdateData(record);
      var updated = db.Timings.Update(v3);
      await db.SaveChangesAsync();
      return updated.Entity.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed updating Timing on '{@Direction}' with: {@Record}", direction, record.ToJson());
      return e;
    }
  }
}
