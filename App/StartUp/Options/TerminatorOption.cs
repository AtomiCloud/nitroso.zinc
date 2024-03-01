using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class TerminatorOption
{
  public const string Key = "Terminator";

  [Required, MinLength(1)] public string QueueName { get; set; } = string.Empty;

}
