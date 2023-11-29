using System.ComponentModel.DataAnnotations;

namespace App.Modules.Schedules.Data;

public class ScheduleData
{
  [Key]
  public DateOnly Date { get; set; }

  public bool Confirmed { get; set; }

  public TimeOnly[] JToWExcluded { get; set; } = null!;

  public TimeOnly[] WToJExcluded { get; set; } = null!;

}
