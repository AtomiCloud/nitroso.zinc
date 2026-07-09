using System.ComponentModel.DataAnnotations;
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
}

// A user allowed to prioritize bookings (when AllowAll is off)
public class PriorityAccessData
{
  [Key]
  [MaxLength(128)]
  public string UserId { get; set; } = string.Empty;

  public DateTime CreatedAt { get; set; }
}
