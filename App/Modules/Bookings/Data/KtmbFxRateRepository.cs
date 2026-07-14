using App.StartUp.Database;
using CSharp_Result;
using Domain.Booking;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

public static class KtmbFxRateMapper
{
  public static KtmbFxRateChange ToChange(this KtmbFxRateData data) =>
    new()
    {
      Id = data.Id,
      Rate = data.Rate,
      EffectiveAt = data.EffectiveAt,
      CreatedAt = data.CreatedAt,
    };
}

public class KtmbFxRateRepository(MainDbContext db, ILogger<KtmbFxRateRepository> logger)
  : IKtmbFxRateRepository
{
  public async Task<Result<IEnumerable<KtmbFxRateChange>>> List()
  {
    try
    {
      var rows = await db.KtmbFxRates.OrderBy(x => x.EffectiveAt).ToArrayAsync();
      return rows.Select(x => x.ToChange()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to list KTMB FX rate changes");
      return e;
    }
  }

  public async Task<Result<KtmbFxRateChange>> Add(decimal rate, DateTime? effectiveAt)
  {
    try
    {
      var now = DateTime.UtcNow;
      logger.LogInformation(
        "Queueing KTMB FX rate change: {Rate}, effective {EffectiveAt}",
        rate,
        effectiveAt ?? now
      );
      // normalize to UTC exactly like KtmbCostRepository.Add: JSON without a
      // Z binds as Unspecified and an offset binds as Local — Npgsql rejects
      // both for timestamptz
      var effective = effectiveAt?.Kind switch
      {
        null => now,
        DateTimeKind.Utc => effectiveAt.Value,
        DateTimeKind.Local => effectiveAt.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(effectiveAt.Value, DateTimeKind.Utc),
      };
      var data = new KtmbFxRateData
      {
        CreatedAt = now,
        EffectiveAt = effective,
        Rate = rate,
      };
      db.KtmbFxRates.Add(data);
      await db.SaveChangesAsync();
      return data.ToChange();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to queue KTMB FX rate change");
      return e;
    }
  }
}
