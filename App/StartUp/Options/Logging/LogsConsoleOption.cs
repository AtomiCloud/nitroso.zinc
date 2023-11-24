using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Logging;

public class LogsConsoleOption
{
  [Required] public bool Enabled { get; set; } = false;

}
