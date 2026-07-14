using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

// Insert-only, effective-dated queue of the admin-entered MYR -> SGD rate
// (SGD per 1 MYR) used to convert actual KTMB-paid amounts in the sales
// analysis (same convention as the KtmbCosts estimate queue): the newest row
// with EffectiveAt <= the booking's CompletedAt is the rate for that
// booking; no effective row = no conversion (the analysis falls back to the
// per-direction estimate).
public class KtmbFxRateData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // the instant this rate starts applying; a future date queues it,
  // CreatedAt (the default) makes it immediate
  public DateTime EffectiveAt { get; set; }

  [Precision(16, 8)]
  public decimal Rate { get; set; }
}
