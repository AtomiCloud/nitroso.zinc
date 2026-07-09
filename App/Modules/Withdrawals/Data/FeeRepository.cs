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
      var latest = await db
        .Fees.OrderByDescending(x => x.CreatedAt)
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

  public async Task<Result<decimal>> SetPercentage(decimal percentage)
  {
    try
    {
      logger.LogInformation(
        "Setting withdrawal fee percentage to {Percentage}",
        percentage
      );
      var data = new FeeData
      {
        CreatedAt = DateTime.UtcNow,
        WithdrawFeePercentage = percentage,
      };
      db.Fees.Add(data);
      await db.SaveChangesAsync();
      return data.WithdrawFeePercentage;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to set withdrawal fee percentage to {Percentage}", percentage);
      return e;
    }
  }
}
