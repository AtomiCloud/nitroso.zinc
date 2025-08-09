using App.StartUp.Options;

namespace App.StartUp.Smtp;

public class SmtpClientFactory(
  IServiceProvider serviceProvider,
  Dictionary<string, SmtpOption> configurations
  ): ISmtpClientFactory
{
  private readonly Dictionary<string, ISmtpClient> _clientCache = [];
  
  public ISmtpClient Get(string key)
  {
    // Return the cached client if available
    if (this._clientCache.TryGetValue(key, out var cachedClient))
      return cachedClient;

    // Create a new client with logger if the configuration exists
    if (!configurations.TryGetValue(key, out var config)) throw new ApplicationException($"Mailbox not found: {key}");
    
    var logger = serviceProvider.GetRequiredService<ILogger<NativeSmtpClient>>();
    var client = new NativeSmtpClient(config, key, logger);
      
    // Cache the client for future requests
    this._clientCache[key] = client;
    return client;
  }
}
