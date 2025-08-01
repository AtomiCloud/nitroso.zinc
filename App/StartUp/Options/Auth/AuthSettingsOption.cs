using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Auth;

public class AuthSettingsOption
{
  [MinLength(1)]
  public string Domain { get; set; } = string.Empty;

  [MinLength(1)]
  public string Audience { get; set; } = string.Empty;

  [MinLength(1)]
  public string Issuer { get; set; } = string.Empty;

  public TokenValidationParametersOption TokenValidation { get; set; } = new();

  public Dictionary<string, AuthPolicyOption>? Policies { get; set; } = null;
}
