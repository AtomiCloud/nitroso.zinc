using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Swagger;

public class OpenApiOption
{
  public const string Key = "Swagger";

  [MinLength(1), Required]
  public string Title { get; set; } = string.Empty;

  [MinLength(1)]
  public string? Description { get; set; }

  public OpenApiContactOption? OpenApiContact { get; set; }

  public OpenApiLicenseOption? OpenApiLicense { get; set; }

  [Url]
  public string? TermsOfService { get; set; }
}
