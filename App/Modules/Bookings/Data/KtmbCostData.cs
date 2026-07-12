using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

// Insert-only, effective-dated queue of what BunnyBooker pays KTMB per
// ticket, per direction (same convention as the withdrawal fee queue): the
// newest row with EffectiveAt <= the booking's CompletedAt and a matching
// Direction is the cost of that booking; no effective row = cost 0.
public class KtmbCostData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // the instant this cost starts applying; a future date queues it,
  // CreatedAt (the default) makes it immediate
  public DateTime EffectiveAt { get; set; }

  // TrainDirection data value: 1 = JToW, 2 = WToJ
  public int Direction { get; set; }

  [Precision(16, 8)]
  public decimal Cost { get; set; }
}
