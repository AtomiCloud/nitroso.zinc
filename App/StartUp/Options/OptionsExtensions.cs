using App.StartUp.Options.Auth;
using App.StartUp.Options.Logging;
using App.StartUp.Options.Metrics;
using App.StartUp.Options.Swagger;
using App.StartUp.Options.Traces;
using App.StartUp.Registry;
using App.Utility;

namespace App.StartUp.Options;

public static class OptionsExtensions
{
  private static readonly string[] CorsPolicies = typeof(CorsPolicies)
    .GetFields()
    .Select(x => x.GetValue(null)?.ToString())
    .Where(x => x is { Length: > 0 })
    .Select(x => x!)
    .ToArray();

  private static readonly string[] Caches = typeof(Caches)
    .GetFields()
    .Select(x => x.GetValue(null)?.ToString())
    .Where(x => x is { Length: > 0 })
    .Select(x => x!)
    .ToArray();

  private static readonly string[] BlockStorages = typeof(BlockStorages)
    .GetFields()
    .Select(x => x.GetValue(null)?.ToString())
    .Where(x => x is { Length: > 0 })
    .Select(x => x!)
    .ToArray();

  private static readonly string[] HttpClients = typeof(HttpClients)
    .GetFields()
    .Select(x => x.GetValue(null)?.ToString())
    .Where(x => x is { Length: > 0 })
    .Select(x => x!)
    .ToArray();

  private static readonly string[] SmtpProviders = typeof(SmtpProviders)
    .GetFields()
    .Select(x => x.GetValue(null)?.ToString())
    .Where(x => x is { Length: > 0 })
    .Select(x => x!)
    .ToArray();

  private static readonly string[] AuthPolicies = typeof(AuthPolicies)
    .GetFields()
    .Select(x => x.GetValue(null)?.ToString())
    .Where(x => x is { Length: > 0 })
    .Select(x => x!)
    .ToArray();

  public static IServiceCollection AddStartupOptions(this IServiceCollection services)
  {
    // Register App Options
    services
      .RegisterOption<AppOption>(AppOption.Key)
      .Validate(
        app => CorsPolicies.Any(x => x == app.DefaultCors),
        "Option App:DefaultCors (Config) must be in CorsPolicies (Class)"
      );

    // Register Domain Options
    services.RegisterOption<DomainOptions>(DomainOptions.Key);

    // Register Swagger Options
    services.RegisterOption<OpenApiOption>(OpenApiOption.Key);

    // Register CountSyncer Options
    services.RegisterOption<CountSyncerOption>(CountSyncerOption.Key);

    // Register CorsOptions
    services
      .RegisterOption<List<CorsOption>>(CorsOption.Key)
      .Validate(
        config => config.All(x => CorsPolicies.Any(f => f == x.Name)),
        "CorsOption.Name (Config File) must be in CorsPolicies (Class)"
      );

    // Register Metrics Options
    services.RegisterOption<MetricOption>(MetricOption.Key);

    // Register Logs Options
    services.RegisterOption<LogsOption>(LogsOption.Key);

    // Register Trace Options
    services.RegisterOption<TraceOption>(TraceOption.Key);

    // Register Database Configurations
    services
      .RegisterOption<Dictionary<string, DatabaseOption>>(DatabaseOption.Key)
      .Validate(
        c => c.All(x => Databases.AcceptedDatabase().Any(d => d == x.Key)),
        "DatabaseOption.Key (Config File) must be in Databases.List (Class)"
      );

    // Register Database Configurations
    services
      .RegisterOption<Dictionary<string, BlockStorageOption>>(BlockStorageOption.Key)
      .Validate(
        c => c.All(x => BlockStorages.Any(d => d == x.Key)),
        "BlockStorage.Key (Config File) must be in BlockStorages (Class)"
      );

    // Register Cache Configurations
    services
      .RegisterOption<Dictionary<string, CacheOption>>(CacheOption.Key)
      .Validate(
        c => c.All(x => Caches.Any(d => d == x.Key)),
        "Cache.Key (Config file) must be in Caches (Class)"
      )
      .Validate(
        c => c.All(x => x.Value.Endpoints.Length > 0),
        "Make sure all Cache Endpoints, 'Cache.<name>.Endpoints' (Config File), has more than 1 element"
      );

    // Register HttpClients
    services
      .RegisterOption<Dictionary<string, HttpClientOption>>(HttpClientOption.Key)
      .Validate(
        c => c.All(x => HttpClients.Any(d => d == x.Key)),
        "HttpClient.Key (Config File) must be in HttpClients (Class)"
      );

    // Register Error Portal Configurations
    services.RegisterOption<ErrorPortalOption>(ErrorPortalOption.Key);

    // Register Auth Configurations
    services
      .RegisterOption<AuthOption>(AuthOption.Key)
      .Validate(
        c => c.Settings?.Policies?.All(x => AuthPolicies.Any(d => d == x.Key)) ?? true,
        "Auth.Settings.Policies.Key (Config File) must be in AuthPolicies (Class)"
      );

    // Encryption Configurations
    services.RegisterOption<EncryptionOption>(EncryptionOption.Key);

    // Payment Configuration
    services.RegisterOption<PaymentOption>(PaymentOption.Key);

    // Register Terminator Options
    services.RegisterOption<TerminatorOption>(TerminatorOption.Key);

    // Register SMTP Options
    services
      .RegisterOption<Dictionary<string, SmtpOption>>(SmtpOption.Key)
      .Validate(
        c => c.All(x => SmtpProviders.Any(d => d == x.Key)),
        "Smtp.Key (Config File) must be in SmtpProviders (Class)"
      );

    return services;
  }
}
