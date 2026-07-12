using System.ComponentModel.DataAnnotations;
using App.Modules.Discounts.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

// Insert-only priority-queue settings (newest CreatedAt wins, like Costs);
// with no row the domain defaults apply (fee 10, allowlist-only, no window)
public class PrioritySettingsData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // Record
  [Precision(16, 8)]
  public decimal Fee { get; set; }

  // true = every user may prioritize; false = allowlisted users only
  public bool AllowAll { get; set; }

  // SGT wall-clock availability window [start, end); both null = always
  public TimeOnly? WindowStartSgt { get; set; }

  public TimeOnly? WindowEndSgt { get; set; }

  // Who gets the boost FREE — owned JSON blob, same shape and storage
  // convention as DiscountData.Target; null = nobody is free
  public DiscountTargetData? FreeTarget { get; set; }

  // Who MAY prioritize at all — when set it takes precedence over
  // AllowAll/PriorityAccessData; null = legacy behavior unchanged
  public DiscountTargetData? AccessTarget { get; set; }
}

// A user allowed to prioritize bookings (when AllowAll is off)
public class PriorityAccessData
{
  [Key]
  [MaxLength(128)]
  public string UserId { get; set; } = string.Empty;

  public DateTime CreatedAt { get; set; }
}
