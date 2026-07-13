using System.ComponentModel.DataAnnotations;
using App.Modules.Discounts.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

// Insert-only priority-queue settings (newest CreatedAt wins, like Costs).
// Post-unification only Policies matters ('[]' = explicit "nobody boosts");
// Policies IS NULL marks a pre-unification row whose legacy columns below
// (fee/allow-all/window/targets + the allowlist table) are synthesized into
// equivalent rules on read.
public class PrioritySettingsData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // the unified ordered policy chain (owned JSON list)
  public List<PriorityPolicyData>? Policies { get; set; }

  // ---- LEGACY (pre-unification) — never written anymore, read only to
  // synthesize rules for old rows ----
  [Precision(16, 8)]
  public decimal Fee { get; set; }

  public bool AllowAll { get; set; }

  public TimeOnly? WindowStartSgt { get; set; }

  public TimeOnly? WindowEndSgt { get; set; }

  public DiscountTargetData? FreeTarget { get; set; }

  public DiscountTargetData? AccessTarget { get; set; }

  public int? SlotCap { get; set; }
}

// One rule inside PrioritySettingsData.Policies (owned JSON). Shape mirrors
// Domain.Booking.PriorityPolicyRecord.
public class PriorityPolicyData
{
  public string Name { get; set; } = string.Empty;

  public bool Allow { get; set; }

  public DiscountTargetData? Target { get; set; }

  public TimeOnly? WindowStartSgt { get; set; }

  public TimeOnly? WindowEndSgt { get; set; }

  public decimal? MinHoursToDeparture { get; set; }

  public decimal? MaxHoursToDeparture { get; set; }

  // "Flat" | "Percent"
  public string FeeKind { get; set; } = "Flat";

  public decimal FeeValue { get; set; }

  public int? SlotCap { get; set; }
}

// A user allowed to prioritize bookings — LEGACY: read only when
// synthesizing rules for pre-unification settings rows
public class PriorityAccessData
{
  [Key]
  [MaxLength(128)]
  public string UserId { get; set; } = string.Empty;

  public DateTime CreatedAt { get; set; }
}
