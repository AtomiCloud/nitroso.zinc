using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Auth;

public class AuthOption
{
  public const string Key = "Auth";

  [Required]
  public bool Enabled { get; set; } = false;

  public AuthSettingsOption? Settings { get; set; } = null;
}
