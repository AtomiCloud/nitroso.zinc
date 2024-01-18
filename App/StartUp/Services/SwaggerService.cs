using App.StartUp.Options.Auth;
using App.StartUp.Services.Swagger;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace App.StartUp.Services;

public static class SwaggerService
{
  public static IServiceCollection AddSwaggerService(this IServiceCollection services, AuthOption auth)
  {
    services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    services.AddSwaggerGen(options =>
    {
      options.OperationFilter<SwaggerDefaultValues>();
      options.SchemaFilter<RequireNonNullablePropertiesSchemaFilter>();
      if (auth.Enabled)
      {
        options.AddSecurityDefinition("Bearer",
          new OpenApiSecurityScheme
          {
            Description =
              @"JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
          });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
          {
            new OpenApiSecurityScheme
            {
              Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
              Scheme = "oauth2",
              Name = "Bearer",
              In = ParameterLocation.Header,
            },
            new List<string>()
          }
        });
      }
    });
    return services;
  }
}
