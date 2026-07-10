using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class RecoveryOption
{
  public const string Key = "Recovery";

  // maximum times a booking may be recycled from Recovering back to Pending
  // (RecoverRevert) before the recycle is refused and a human must resolve it
  [Required, Range(0, 1000)]
  public int MaxRetries { get; set; } = 10;
}
