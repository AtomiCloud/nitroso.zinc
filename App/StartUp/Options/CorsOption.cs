using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class CorsOption
{
  public const string Key = "Cors";

  [Required, MinLength(1)]
  public string Name { get; set; } = string.Empty;

  public string[]? Origins { get; set; } = null;

  public string[]? Headers { get; set; } = null;

  public string[]? Methods { get; set; } = null;

  public uint? PreflightMaxAge { get; set; } = null;

  public bool? SupportCredentials { get; set; } = null;
}
