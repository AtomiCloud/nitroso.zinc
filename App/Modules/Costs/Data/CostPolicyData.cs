using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Costs.Data;

// A pricing rule row: every non-null Match* dimension must equal the
// booking's for the (signed) Amount to be added to the base cost
public class CostPolicyData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // Record
  [MaxLength(256)]
  public string Name { get; set; } = string.Empty;

  public bool Enabled { get; set; }

  public DateOnly? MatchDate { get; set; }

  public TimeOnly? MatchTime { get; set; }

  // Domain DayOfWeek: 0 = Sunday ... 6 = Saturday
  public byte? MatchDayOfWeek { get; set; }

  // TrainDirection via TimingMapper.ToData: 1 = JToW, 2 = WToJ
  public int? MatchDirection { get; set; }

  public int? LeadTimeUnderHours { get; set; }

  // SIGNED: negative = discount; percent of base cost when IsPercentage
  [Precision(16, 8)]
  public decimal Amount { get; set; }

  public bool IsPercentage { get; set; }

  // active window [EffectiveAt, ExpiresAt); null = unbounded on that side
  public DateTime? EffectiveAt { get; set; }

  public DateTime? ExpiresAt { get; set; }
}
