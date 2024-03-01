using App.Modules.System;
using App.StartUp.Options;
using App.StartUp.Registry;
using App.Utility;
using CSharp_Result;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace App.Modules.Payments.Airwallex;

public interface IGatewayAuthenticator
{
  Task<Result<string>> GetToken();
}

public class AirwallexAuthenticator(
  IRedisClientFactory factory,
  IHttpClientFactory httpClientsFactory,
  IEncryptor encryptor,
  IMemoryCache localCache,
  ILogger<AirwallexAuthenticator> logger,
  IOptions<PaymentOption> o) : IGatewayAuthenticator
{
  private const string AirWallexKey = "airwallex_auth_token";
  private IRedisDatabase Redis => factory.GetRedisClient(Caches.Main).Db0;
  private HttpClient HttpClient => httpClientsFactory.CreateClient(HttpClients.Airwallex);

  private async Task<Result<(string, DateTime)>> getToken()
  {
    try
    {
      var request = new HttpRequestMessage
      {
        Method = HttpMethod.Post,
        RequestUri = new Uri("api/v1/authentication/login", UriKind.Relative),
        Headers = { { "x-client-id", o.Value.Airwallex.ClientId }, { "x-api-key", o.Value.Airwallex.ApiKey }, },
      };
      using var response = await this.HttpClient.SendAsync(request);

      var body = await response.Content.ReadAsStringAsync();

      try
      {
        response.EnsureSuccessStatusCode();
      }
      catch (HttpRequestException e)
      {
        logger.LogError(e, "Failed to authenticate with Airwallex (HTTP Error), Response: {Body}", body);
        return e;
      }
      catch (Exception e)
      {
        logger.LogError(e, "Failed to authenticate with Airwallex");
        throw;
      }

      var r = body.ToObj<AirwallexAuthTokenRes>();

      var expiry = DateTime.Parse(r.ExpiresAt);
      return (r.Token, expiry);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to authenticate with Airwallex");
      throw;
    }
  }

  public async Task<Result<(string, DateTime)?>> recall()
  {
    localCache.TryGetValue(AirWallexKey, out AuthenticatorToken? token);
    if (token is null || token.Expiry <= DateTime.Now)
    {
      token = await this.Redis.GetAsync<AuthenticatorToken>(AirWallexKey);
      if (token is null || token.Expiry <= DateTime.Now) return ((string, DateTime)?)null;
      localCache.Set(AirWallexKey, token);
    }

    var d = encryptor.Decrypt(token.Secret);
    return (d, token.Expiry);
  }

  public async Task<Result<Unit>> remember(string token, DateTime expiry)
  {
    var tokenCipher = encryptor.Encrypt(token);

    var model = new AuthenticatorToken(tokenCipher, expiry);
    localCache.Set(AirWallexKey, model);
    await this.Redis.AddAsync(AirWallexKey, model);
    return new Unit();
  }

  public Task<Result<string>> GetToken()
  {
    return this.recall()
      .ThenAwait(x => x == null
        ? this.getToken()
          .DoAwait(DoType.MapErrors, r => this.remember(r.Item1, r.Item2))
        : Task.FromResult(new Result<(string, DateTime)>((x!.Value.Item1, x.Value.Item2)))
      )
      .Then(x => x.Item1, Errors.MapNone);
  }
}
