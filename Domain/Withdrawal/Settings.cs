using CSharp_Result;

namespace Domain.Withdrawal;

// Availability of the PayNow rail for NEW withdrawals: always, never, or only
// when the card rail cannot cover the requested amount (fallback)
public enum PayNowMode
{
  Enabled = 0,
  Disabled = 1,

  // PayNow is accepted only when the refundable pool cannot cover the
  // requested amount — card refunds are the preferred rail
  FallbackOnly = 2,
}

// Admin-editable withdrawal method policy and the tin sweep switch
// (insert-only, newest row wins — same pattern as PrioritySettings). With no
// row the defaults apply.
public record WithdrawalSettingsRecord
{
  // may users create card-refund withdrawals
  public required bool CardRefundEnabled { get; init; }

  // when may users create PayNow withdrawals
  public required PayNowMode PayNowMode { get; init; }

  // runtime switch for tin's automated withdrawal sweep (tin polls
  // GET Withdrawal/settings/current and skips the sweep when off)
  public required bool SweepEnabled { get; init; }

  // matches the deployed reality before this table existed: both rails were
  // live (PayNow as the fallback), and the tin nightly sweep is disabled
  public static readonly WithdrawalSettingsRecord Default = new()
  {
    CardRefundEnabled = true,
    PayNowMode = PayNowMode.FallbackOnly,
    SweepEnabled = false,
  };
}

public record WithdrawalSettingsPrincipal
{
  public required Guid Id { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required WithdrawalSettingsRecord Record { get; init; }
}

public interface IWithdrawalSettingsRepository
{
  // the newest settings row, or null when none was ever written (defaults apply)
  Task<Result<WithdrawalSettingsPrincipal?>> GetCurrent();

  Task<Result<WithdrawalSettingsPrincipal>> Create(WithdrawalSettingsRecord record);
}
