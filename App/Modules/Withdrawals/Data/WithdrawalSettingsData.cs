namespace App.Modules.Withdrawals.Data;

// Insert-only withdrawal method policy + tin sweep switch (newest CreatedAt
// wins, like PrioritySettings); with no row the domain defaults apply
// (CardRefundEnabled, PayNow fallback-only, sweep off)
public class WithdrawalSettingsData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // Record
  // may users create card-refund withdrawals
  public bool CardRefundEnabled { get; set; }

  // 0 = Enabled, 1 = Disabled, 2 = FallbackOnly (accepted only when the
  // refundable pool cannot cover the requested amount)
  public byte PayNowMode { get; set; }

  // runtime switch for tin's automated withdrawal sweep
  public bool SweepEnabled { get; set; }
}
