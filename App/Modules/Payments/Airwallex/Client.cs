using System.Net.Http.Headers;
using App.StartUp.Registry;
using App.Utility;
using CSharp_Result;

namespace App.Modules.Payments.Airwallex;

public class AirWallexClient(
  IHttpClientFactory factory,
  IGatewayAuthenticator authenticator,
  ILogger<AirWallexClient> logger
)
{
  private HttpClient HttpClient => factory.CreateClient(HttpClients.Airwallex);

  public Task<Result<AirwallexCreateIntentRes>> CreateIntent(
    AirwallexCreateIntentReq req)
  {
    return authenticator.GetToken()
      .ThenAwait(async token =>
      {
        var request = new HttpRequestMessage
        {
          Method = HttpMethod.Post,
          RequestUri = new Uri("api/v1/pa/payment_intents/create", UriKind.Relative),
          Headers =
          {
            Authorization = new AuthenticationHeaderValue("Bearer", token)
          },
          Content = JsonContent.Create(req),
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
        return body.ToObj<AirwallexCreateIntentRes>().ToResult();
      });
  }
}
