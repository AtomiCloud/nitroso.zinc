using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class CacheOption
{
  public const string Key = "Cache";

  public string? User { get; set; } = null;

  [Required]
  public bool AbortConnect { get; set; } = true;

  [Required]
  public bool AllowAdmin { get; set; } = false;

  [Required, Range(0, int.MaxValue)]
  public int ConnectRetry { get; set; } = 3;

  [Required, Range(0, int.MaxValue)]
  public int ConnectTimeout { get; set; } = 5000;

  [Required]
  public bool SSL { get; set; } = false;

  [Required]
  public string Password { get; set; } = string.Empty;

  [Required, Range(0, int.MaxValue)]
  public int SyncTimeout { get; set; } = 0;

  [Required]
  public string[] Endpoints { get; set; } = [];
}
