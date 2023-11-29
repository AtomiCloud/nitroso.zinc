using System.ComponentModel.DataAnnotations;

namespace App.Modules.Timings.Data;

public class TimingData
{
  [Key] public int Direction { get; set; }

  public TimeOnly[] Timings { get; set; } = null!;
}

