using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class AppOption
{
  public const string Key = "App";

  [Required, MinLength(2)] public string Landscape { get; set; } = string.Empty;

  [Required, MinLength(2)] public string Platform { get; set; } = string.Empty;

  [Required, MinLength(2)] public string Service { get; set; } = string.Empty;

  [Required, MinLength(2)] public string Module { get; set; } = string.Empty;

  [Required, RegularExpression(@"\d+\.\d+\.\d+")]
  public string Version { get; set; } = string.Empty;

  [Required] public bool GenerateConfig { get; set; } = true;

  [Required] public bool EnableSwagger { get; set; } = true;

  [Required, MinLength(1)] public string DefaultCors { get; set; } = string.Empty;

  [Required, AllowedValues("Server", "Migration")]
  public string Mode { get; set; } = string.Empty;
}
