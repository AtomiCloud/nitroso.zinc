using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class DatabaseOption
{
  public const string Key = "Database";

  [Required, MinLength(1)] public string Host { get; set; } = string.Empty;

  [Required, MinLength(1)] public string User { get; set; } = string.Empty;

  [Required, MinLength(1)] public string Password { get; set; } = string.Empty;

  [Required, Range(0, ushort.MaxValue)] public int Port { get; set; } = 0;

  [Required, MinLength(1)] public string Database { get; set; } = string.Empty;

  [Required] public bool AutoMigrate { get; set; } = false;

  [Required, Range(0, int.MaxValue)] public int Timeout { get; set; } = 30;
}
