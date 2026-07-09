using System.ComponentModel.DataAnnotations;

namespace App.Modules.Milestones.Data;

// Admin-managed markers for snatching-algorithm changes; the stats UI
// defaults its range start to the latest milestone
public class MilestoneData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // the date the change took effect
  public DateOnly Date { get; set; }

  [MaxLength(256)]
  public string Label { get; set; } = string.Empty;
}
