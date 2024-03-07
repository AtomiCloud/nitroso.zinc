using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class TerminatorOption
{
  public const string Key = "Terminator";

  [Required, MinLength(1)] public string QueueName { get; set; } = string.Empty;

  [Required, Range(0, 3600)] public int MinBuffer { get; set; } = 0;
}
