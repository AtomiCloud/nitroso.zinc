using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Auth;

public class AuthPolicyOption
{
  [Required, AllowedValues("Any", "All")]
  public string Type { get; set; } = "All";

  [Required, AllowedValues("scope", "roles", "permissions")]
  public string Field { get; set; } = "scope";

  [Required]
  public string[] Target { get; set; } = [];
}
