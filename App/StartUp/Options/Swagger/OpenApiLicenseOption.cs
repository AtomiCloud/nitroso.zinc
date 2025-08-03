using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Swagger;

public class OpenApiLicenseOption
{
  [MinLength(1)]
  public string? Name { get; set; }

  [Url]
  public string? Url { get; set; }
}
