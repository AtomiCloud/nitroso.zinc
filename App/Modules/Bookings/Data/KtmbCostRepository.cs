using App.Modules.Timings.Data;
using App.StartUp.Database;
using CSharp_Result;
using Domain.Booking;
using Domain.Timings;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

public static class KtmbCostMapper
{
  public static KtmbCostChange ToChange(this KtmbCostData data) =>
    new()
    {
      Id = data.Id,
      Direction = data.Direction.ToTrainDirection(),
      Cost = data.Cost,
      EffectiveAt = data.EffectiveAt,
      CreatedAt = data.CreatedAt,
    };
}

public class KtmbCostRepository(MainDbContext db, ILogger<KtmbCostRepository> logger)
  : IKtmbCostRepository
{
  public async Task<Result<IEnumerable<KtmbCostChange>>> List()
  {
    try
    {
      var rows = await db.KtmbCosts.OrderBy(x => x.EffectiveAt).ToArrayAsync();
      return rows.Select(x => x.ToChange()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to list KTMB cost changes");
      return e;
    }
  }

  public async Task<Result<KtmbCostChange>> Add(
    TrainDirection direction,
    decimal cost,
    DateTime? effectiveAt
  )
  {
    try
    {
      var now = DateTime.UtcNow;
      logger.LogInformation(
        "Queueing KTMB cost change: {Direction} -> {Cost}, effective {EffectiveAt}",
        direction,
        cost,
        effectiveAt ?? now
      );
      // normalize to UTC exactly like FeeRepository.Add: JSON without a Z
      // binds as Unspecified and an offset binds as Local — Npgsql rejects
      // both for timestamptz
      var effective = effectiveAt?.Kind switch
      {
        null => now,
        DateTimeKind.Utc => effectiveAt.Value,
        DateTimeKind.Local => effectiveAt.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(effectiveAt.Value, DateTimeKind.Utc),
      };
      var data = new KtmbCostData
      {
        CreatedAt = now,
        EffectiveAt = effective,
        Direction = direction.ToData(),
        Cost = cost,
      };
      db.KtmbCosts.Add(data);
      await db.SaveChangesAsync();
      return data.ToChange();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to queue KTMB cost change");
      return e;
    }
  }
}
