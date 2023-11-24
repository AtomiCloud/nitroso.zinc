using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class HttpClientOption
{
  public const string Key = "HttpClient";

  [Required, Url] public string BaseAddress { get; set; } = string.Empty;

  [Required, Range(0, int.MaxValue)] public int Timeout { get; set; } = 30;

  public string? BearerAuth { get; set; } = null;
}
