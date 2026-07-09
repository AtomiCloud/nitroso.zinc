using App.StartUp.Database;
using CSharp_Result;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Withdrawals.Data;

public class FeeRepository(MainDbContext db, ILogger<FeeRepository> logger) : IFeeRepository
{
  public async Task<Result<decimal?>> GetLatestPercentage()
  {
    try
    {
      var now = DateTime.UtcNow;
      var latest = await db
        .Fees.Where(x => x.EffectiveAt <= now)
        .OrderByDescending(x => x.EffectiveAt)
        .ThenByDescending(x => x.CreatedAt)
        .ThenByDescending(x => x.Id)
        .FirstOrDefaultAsync();
      return latest?.WithdrawFeePercentage;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to read the latest withdrawal fee percentage");
      return e;
    }
  }

  public async Task<Result<IEnumerable<FeeChange>>> GetUpcoming()
  {
    try
    {
      var now = DateTime.UtcNow;
      var upcoming = await db
        .Fees.Where(x => x.EffectiveAt > now)
        .OrderBy(x => x.EffectiveAt)
        .ToArrayAsync();
      return upcoming
        .Select(x => new FeeChange
        {
          Percentage = x.WithdrawFeePercentage,
          EffectiveAt = x.EffectiveAt,
        })
        .ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to read upcoming withdrawal fee changes");
      return e;
    }
  }

  public async Task<Result<FeeChange>> SetPercentage(decimal percentage, DateTime? effectiveAt)
  {
    try
    {
      var now = DateTime.UtcNow;
      logger.LogInformation(
        "Setting withdrawal fee percentage to {Percentage} effective {EffectiveAt}",
        percentage,
        effectiveAt ?? now
      );
      var data = new FeeData
      {
        CreatedAt = now,
        EffectiveAt = effectiveAt ?? now,
        WithdrawFeePercentage = percentage,
      };
      db.Fees.Add(data);
      await db.SaveChangesAsync();
      return new FeeChange
      {
        Percentage = data.WithdrawFeePercentage,
        EffectiveAt = data.EffectiveAt,
      };
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to set withdrawal fee percentage to {Percentage}", percentage);
      return e;
    }
  }
}
