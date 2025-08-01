using App.StartUp.Options;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace App.StartUp.Services;

public static class CorsService
{
  private static void AddPreflightMaxAge(this CorsPolicyBuilder builder, uint? preflightMaxAge)
  {
    if (preflightMaxAge.HasValue)
      builder.SetPreflightMaxAge(TimeSpan.FromSeconds(preflightMaxAge.Value));
  }

  private static void AddSupportCredentials(
    this CorsPolicyBuilder builder,
    bool? supportCredentials
  )
  {
    if (!supportCredentials.HasValue)
      return;
    if (supportCredentials.Value)
      builder.AllowCredentials();
    else
      builder.DisallowCredentials();
  }

  private static void AddOrigins(this CorsPolicyBuilder builder, string[]? origins)
  {
    if (origins == null)
      builder.AllowAnyOrigin();
    else
      builder.WithOrigins(origins);
  }

  private static void AddHeaders(this CorsPolicyBuilder builder, string[]? headers)
  {
    if (headers == null)
      builder.AllowAnyHeader();
    else
      builder.WithHeaders(headers);
  }

  private static void AddMethods(this CorsPolicyBuilder builder, string[]? methods)
  {
    if (methods == null)
      builder.AllowAnyMethod();
    else
      builder.WithMethods(methods);
  }

  private static void BuildPolicy(this CorsOptions native, CorsOption config)
  {
    native.AddPolicy(
      config.Name,
      build =>
      {
        build.AddPreflightMaxAge(config.PreflightMaxAge);
        build.AddHeaders(config.Headers);
        build.AddMethods(config.Methods);
        build.AddOrigins(config.Origins);
        build.AddSupportCredentials(config.SupportCredentials);
      }
    );
  }

  public static IServiceCollection AddCorsService(
    this IServiceCollection services,
    List<CorsOption> cors
  )
  {
    services.AddCors(options =>
    {
      foreach (var c in cors)
        options.BuildPolicy(c);
    });
    return services;
  }
}
