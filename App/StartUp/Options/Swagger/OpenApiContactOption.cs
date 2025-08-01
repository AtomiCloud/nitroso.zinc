using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Swagger;

public class OpenApiContactOption
{
  [MinLength(1)]
  public string? Name { get; set; }

  [EmailAddress]
  public string? Email { get; set; }

  [Url]
  public string? Url { get; set; }
}
