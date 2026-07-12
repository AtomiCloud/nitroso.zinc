using CSharp_Result;
using Domain.Withdrawal;

namespace UnitTest.Withdrawals;

// Shared fake for the insert-only withdrawal settings store: null = no row
// was ever written (the service falls back to WithdrawalSettingsRecord.Default)
internal sealed class FakeWithdrawalSettingsRepository(WithdrawalSettingsRecord? settings)
  : IWithdrawalSettingsRepository
{
  public WithdrawalSettingsRecord? Settings { get; set; } = settings;

  public List<WithdrawalSettingsRecord> Created { get; } = [];

  public int GetCurrentCalls { get; private set; }

  public Task<Result<WithdrawalSettingsPrincipal?>> GetCurrent()
  {
    GetCurrentCalls++;
    return Task.FromResult<Result<WithdrawalSettingsPrincipal?>>(
      Settings == null
        ? (WithdrawalSettingsPrincipal?)null
        : new WithdrawalSettingsPrincipal
        {
          Id = Guid.NewGuid(),
          CreatedAt = DateTime.UtcNow,
          Record = Settings,
        }
    );
  }

  public Task<Result<WithdrawalSettingsPrincipal>> Create(WithdrawalSettingsRecord record)
  {
    Created.Add(record);
    Settings = record;
    return Task.FromResult<Result<WithdrawalSettingsPrincipal>>(
      new WithdrawalSettingsPrincipal
      {
        Id = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        Record = record,
      }
    );
  }
}
