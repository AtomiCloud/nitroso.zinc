using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Traces;

public class TraceConsoleOption
{
  [Required]
  public bool Enabled { get; set; } = false;
}
