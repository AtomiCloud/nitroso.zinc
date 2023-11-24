using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Auth;

public class TokenValidationParametersOption
{
  [Required] public bool ValidateIssuer { get; set; } = true;
  [Required] public bool ValidateAudience { get; set; } = true;

  [Required] public int ClockSkew { get; set; } = 0;

  [Required] public bool ValidateIssuerSigningKey { get; set; } = true;

  [Required] public bool ValidateLifetime { get; set; } = true;
}
