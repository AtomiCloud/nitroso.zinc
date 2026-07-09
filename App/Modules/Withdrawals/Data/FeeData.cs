using Microsoft.EntityFrameworkCore;

namespace App.Modules.Withdrawals.Data;

// Insert-only history of withdrawal fee rates; the newest row is the live
// rate. When the table is empty the configured Domain:WithdrawFeePercentage
// acts as the fallback, so deploys behave identically until an admin sets a
// rate.
public class FeeData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // the instant this rate starts applying; a future date schedules the
  // change, CreatedAt (the default) makes it immediate
  public DateTime EffectiveAt { get; set; }

  // percent of the withdrawn amount, e.g. 4 = 4%
  [Precision(16, 8)]
  public decimal WithdrawFeePercentage { get; set; }
}
