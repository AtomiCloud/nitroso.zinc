using System.Text;
using App.StartUp.Options.Swagger;
using App.Utility;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace App.StartUp.Services.Swagger;

/// <summary>
/// Configures the Swagger generation options.
/// </summary>
/// <remarks>This allows API versioning to define a Swagger document per API version after the
/// <see cref="IApiVersionDescriptionProvider"/> service has been resolved from the service container.</remarks>
public class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider,
    IOptionsMonitor<OpenApiOption> swaggerConfig)
  : IConfigureOptions<SwaggerGenOptions>
{
  /// <inheritdoc />
  public void Configure(SwaggerGenOptions options)
  {
    foreach (var description in provider.ApiVersionDescriptions)
    {
      options.SwaggerDoc(description.GroupName, this.CreateInfoForApiVersion(description));
    }
  }

  private OpenApiInfo Info => new()
  {
    Title = swaggerConfig.CurrentValue.Title,
    Contact = swaggerConfig.CurrentValue.OpenApiContact?.ToDomain(),
    License = swaggerConfig.CurrentValue.OpenApiLicense?.ToDomain(),
    TermsOfService = swaggerConfig.CurrentValue.TermsOfService?.ToUri(),
  };

  private StringBuilder BuildPolicyDescription(StringBuilder text, SunsetPolicy policy)
  {
    if (policy.Date is { } when)
    {
      text.Append(" The API will be sunset on ")
        .Append(when.Date.ToShortDateString())
        .Append('.');
    }

    if (!policy.HasLinks) return text;
    text.AppendLine();
    foreach (var link in policy.Links)
    {
      if (link.Type != "text/html") continue;
      text.AppendLine();
      if (link.Title.HasValue) text.Append(link.Title.Value).Append(": ");
      text.Append(link.LinkTarget.OriginalString);
    }

    return text;
  }

  private string Description(ApiVersionDescription description)
  {
    var text = new StringBuilder(swaggerConfig.CurrentValue.Description);
    if (description.IsDeprecated) text.Append(" This API version has been deprecated.");
    if (description.SunsetPolicy is { } policy) text = this.BuildPolicyDescription(text, policy);
    return text.ToString();
  }

  private OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
  {
    var info = this.Info;
    info.Version = description.ApiVersion.ToString();
    info.Description = this.Description(description);
    return info;
  }
}
