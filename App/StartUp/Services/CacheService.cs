using App.StartUp.Options;
using Kirinnee.Helper;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.System.Text.Json;

namespace App.StartUp.Services;

public static class CacheService
{
  public static IServiceCollection AddCache(this IServiceCollection service, Dictionary<string, CacheOption> o)
  {
    var config = o.Select(kv =>
    {
      var k = kv.Key;
      var s = kv.Value;
      var e = s.Endpoints.JoinBy(",");
      return new RedisConfiguration
      {
        Name = k,
        Hosts =
          s.Endpoints.Select(x => new RedisHost { Host = x.Split(":")[0], Port = int.Parse(x.Split(":")[1]) })
            .ToArray(),
        AbortOnConnectFail = s.AbortConnect,
        User = s.User,
        AllowAdmin = s.AllowAdmin,
        Password = s.Password,
        ConnectTimeout = s.ConnectTimeout,
        SyncTimeout = s.SyncTimeout,
        Ssl = s.SSL,
      };
    }).ToArray();
    if (config.Length > 0)
    {
      config[0].IsDefault = true;
      service.AddStackExchangeRedisExtensions<SystemTextJsonSerializer>(config);
    }

    return service;
  }
}
