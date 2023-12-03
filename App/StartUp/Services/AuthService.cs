using System.Security.Claims;
using App.StartUp.Options.Auth;
using App.StartUp.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace App.StartUp.Services;

public static class AuthService
{
  public static WebApplication UseAuthService(this WebApplication app)
  {
    app.UseAuthentication();
    app.UseAuthorization();
    return app;
  }

  public static IServiceCollection AddAuthService(this IServiceCollection services, AuthOption o)
  {
    if (o.Settings is null) throw new ApplicationException("Auth is enabled but Domain or Audience is null");

    services.AddSingleton<IAuthorizationHandler, HasAnyHandler>()
      .AutoTrace<IAuthorizationHandler>();

    services.AddSingleton<IAuthorizationHandler, HasAllHandler>()
      .AutoTrace<IAuthorizationHandler>();

    services.AddSingleton<IAuthHelper, AuthHelper>()
      .AutoTrace<IAuthHelper>();

    var s = o.Settings!;
    var domain = $"https://{s.Domain}";
    services
      .AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
      })
      .AddJwtBearer(options =>
      {
        options.Authority = domain;
        options.Audience = s.Audience;
        if (s.TokenValidation is { } to)
        {
          options.TokenValidationParameters = new TokenValidationParameters
          {
            ValidIssuer = s.Issuer,
            NameClaimType = ClaimTypes.NameIdentifier,
            ValidateIssuer = to.ValidateIssuer,
            ValidateAudience = to.ValidateAudience,
            ClockSkew = TimeSpan.FromSeconds(to.ClockSkew),
            ValidateIssuerSigningKey = to.ValidateIssuerSigningKey,
            ValidateLifetime = to.ValidateLifetime,
          };
        }
        else
        {
          options.TokenValidationParameters =
            new TokenValidationParameters { NameClaimType = ClaimTypes.NameIdentifier };
        }
      });

    var p = s.Policies ?? [];

    services.AddAuthorization(opt =>
    {
      foreach (var (k, v) in p)
      {
        opt.AddPolicy(k, pb =>
        {
          switch (v)
          {
            case { Type: "Any" }:
              pb.Requirements.AddAnyScope(s.Issuer, v.Field, v.Target);
              break;
            case { Type: "All" }:
              pb.Requirements.AddAllScope(s.Issuer, v.Field, v.Target);
              break;
            default:
              throw new ApplicationException($"Auth Policy Type is not supported: {v.Type}");
          }
        });
      }
    });

    return services;
  }
}
