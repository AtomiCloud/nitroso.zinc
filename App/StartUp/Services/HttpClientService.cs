using System.Net.Http.Headers;
using App.StartUp.Options;

namespace App.StartUp.Services;

public static class HttpClientService
{
  public static IServiceCollection AddHttpClientService(
    this IServiceCollection service,
    Dictionary<string, HttpClientOption> opt
  )
  {
    foreach (var (k, v) in opt)
    {
      service.AddHttpClient(
        k,
        c =>
        {
          c.Timeout = TimeSpan.FromSeconds(v.Timeout);
          c.BaseAddress = new Uri(v.BaseAddress);
          if (v.BearerAuth is { Length: > 0 })
          {
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
              "Bearer",
              v.BearerAuth
            );
          }
        }
      );
    }

    return service;
  }
}
