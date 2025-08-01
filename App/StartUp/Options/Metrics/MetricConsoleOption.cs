using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Metrics;

public class MetricConsoleOption
{
  [Required]
  public bool Enabled { get; set; } = false;

  [Required, Range(0, int.MaxValue)]
  public int ExportInterval { get; set; } = 0;
}
