using App.StartUp.Options.Swagger;
using App.Utility;
using Microsoft.OpenApi.Models;

namespace App.StartUp.Services.Swagger;

public static class SwaggerConfigMapper
{
  public static OpenApiContact ToDomain(this OpenApiContactOption apiContact)
  {
    return new OpenApiContact
    {
      Name = apiContact.Name,
      Email = apiContact.Email,
      Url = apiContact.Url?.ToUri(),
    };
  }

  public static OpenApiLicense ToDomain(this OpenApiLicenseOption apiLicense)
  {
    return new OpenApiLicense { Name = apiLicense.Name, Url = apiLicense.Url?.ToUri(), };
  }
}
