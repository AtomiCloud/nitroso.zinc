using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class CountSyncerOption
{
  public const string Key = "CountSyncer";

  [Required, MinLength(1)] public string StreamName { get; set; } = string.Empty;

  [Required] public uint StreamLength { get; set; } = 50;
}
