using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Discounts.Data;

public class DiscountData
{
  public Guid Id { get; set; }

  // Record
  [Precision(16, 8)]
  public decimal Amount { get; set; }

  public string DiscountType { get; set; } = string.Empty;

  public string Name { get; set; } = string.Empty;

  public string Description { get; set; } = string.Empty;

  // Slot matchers, shaped like CostPolicyData: every non-null Match* must
  // equal the priced slot's for the discount to apply. Top-level columns
  // (not part of the Target JSON blob, which stays user/role only).
  public DateOnly? MatchDate { get; set; }

  public TimeOnly? MatchTime { get; set; }

  // Domain DayOfWeek: 0 = Sunday ... 6 = Saturday
  public byte? MatchDayOfWeek { get; set; }

  // TrainDirection via TimingMapper.ToData: 1 = JToW, 2 = WToJ
  public int? MatchDirection { get; set; }

  // inclusive early-purchase threshold; null = any lead time
  // Keep the legacy physical column name so old and new API pods can run
  // together during rolling deployments and rollbacks.
  [Column("LeadTimeUnderHours")]
  public int? LeadTimeAtLeastHours { get; set; }

  // active window [EffectiveAt, ExpiresAt); null = unbounded on that side
  public DateTime? EffectiveAt { get; set; }

  public DateTime? ExpiresAt { get; set; }

  // Target
  public DiscountTargetData Target { get; set; } = new();

  // Status
  public bool Disabled { get; set; }
}

public class DiscountTargetData
{
  public string MatchMode { get; set; } = string.Empty;

  public List<DiscountMatchData> Matches { get; set; } = [];
}

public class DiscountMatchData
{
  public string Value { get; set; } = string.Empty;

  public string Type { get; set; } = string.Empty;
}
