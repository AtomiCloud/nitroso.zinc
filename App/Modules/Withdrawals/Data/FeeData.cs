using Microsoft.EntityFrameworkCore;

namespace App.Modules.Withdrawals.Data;

// Insert-only queue of fee change events per fee type; the newest row whose
// EffectiveAt has passed is the live fee, future rows are the queue. With no
// effective row the fee is zero-zero — no fee exists until an admin adds one.
public class FeeData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // the instant this change starts applying; a future date queues it,
  // CreatedAt (the default) makes it immediate
  public DateTime EffectiveAt { get; set; }

  // Domain.FeeType: 0 = Withdrawal, 1 = Deposit, 2 = Termination
  public byte Type { get; set; }

  // percent of the amount, e.g. 4 = 4%
  [Precision(16, 8)]
  public decimal Percentage { get; set; }

  // flat SGD component added on top of the percentage
  [Precision(16, 8)]
  public decimal FlatAmount { get; set; }

  // absolute SGD ceiling on the computed fee; null = uncapped
  [Precision(16, 8)]
  public decimal? Cap { get; set; }
}
