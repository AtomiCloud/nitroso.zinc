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

  // Ordered policy chain (see Domain.Booking.PriorityPolicyRecord) — owned
  // JSON list; NULL/empty = no policies, legacy gate only
  public List<PriorityPolicyData>? Policies { get; set; }

  // Max priority bookings per timeslot; NULL = uncapped
  public int? SlotCap { get; set; }
}

// One policy rule inside PrioritySettingsData.Policies (owned JSON)
public class PriorityPolicyData
{
  public string Name { get; set; } = string.Empty;

  public bool Allow { get; set; }

  public DiscountTargetData? Target { get; set; }

  public decimal? MinHoursToDeparture { get; set; }

  public decimal? MaxHoursToDeparture { get; set; }

  public decimal? FeeOverride { get; set; }
}

// A user allowed to prioritize bookings (when AllowAll is off)
public class PriorityAccessData
{
  [Key]
  [MaxLength(128)]
  public string UserId { get; set; } = string.Empty;

  public DateTime CreatedAt { get; set; }
}
