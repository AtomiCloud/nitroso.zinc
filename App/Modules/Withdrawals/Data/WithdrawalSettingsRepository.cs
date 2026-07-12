using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Withdrawal;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Withdrawals.Data;

public class WithdrawalSettingsRepository(
  MainDbContext db,
  ILogger<WithdrawalSettingsRepository> logger
) : IWithdrawalSettingsRepository
{
  public async Task<Result<WithdrawalSettingsPrincipal?>> GetCurrent()
  {
    try
    {
      var r = await db
        .WithdrawalSettings.OrderByDescending(x => x.CreatedAt)
        .ThenByDescending(x => x.Id)
        .FirstOrDefaultAsync();
      return r?.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to read the current withdrawal settings");
      return e;
    }
  }

  public async Task<Result<WithdrawalSettingsPrincipal>> Create(WithdrawalSettingsRecord record)
  {
    try
    {
      logger.LogInformation("Creating WithdrawalSettings: {@Record}", record.ToJson());
      var data = new WithdrawalSettingsData { CreatedAt = DateTime.UtcNow };
      data.UpdateData(record);
      var r = db.WithdrawalSettings.Add(data);
      await db.SaveChangesAsync();
      return r.Entity.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create WithdrawalSettings {@Record}", record.ToJson());
      return e;
    }
  }
}

public static class WithdrawalSettingsDataMapper
{
  // Data -> Domain
  public static WithdrawalSettingsRecord ToRecord(this WithdrawalSettingsData data) =>
    new()
    {
      CardRefundEnabled = data.CardRefundEnabled,
      PayNowMode = (PayNowMode)data.PayNowMode,
      SweepEnabled = data.SweepEnabled,
    };

  public static WithdrawalSettingsPrincipal ToPrincipal(this WithdrawalSettingsData data) =>
    new()
    {
      Id = data.Id,
      CreatedAt = data.CreatedAt,
      Record = data.ToRecord(),
    };

  // Domain -> Data
  public static WithdrawalSettingsData UpdateData(
    this WithdrawalSettingsData data,
    WithdrawalSettingsRecord record
  )
  {
    data.CardRefundEnabled = record.CardRefundEnabled;
    data.PayNowMode = (byte)record.PayNowMode;
    data.SweepEnabled = record.SweepEnabled;
    return data;
  }
}
