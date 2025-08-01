using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class EncryptionOption
{
  public const string Key = "Encryption";

  [Required]
  public string Secret { get; set; } = string.Empty;
}
