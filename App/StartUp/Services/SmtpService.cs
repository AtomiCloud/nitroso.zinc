using App.StartUp.Options;
using App.StartUp.Smtp;

namespace App.StartUp.Services;

public static class SmtpService
{
  public static IServiceCollection AddSmtp(
    this IServiceCollection services,
    Dictionary<string, SmtpOption> o
  )
  {
    // Register a factory with deferred service provider injection
    services.AddSingleton<ISmtpClientFactory>(serviceProvider =>
      new SmtpClientFactory(serviceProvider, o)).AutoTrace<ISmtpClientFactory>();

    return services;
  }
}
