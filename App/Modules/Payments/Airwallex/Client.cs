using System.Net.Http.Headers;
using App.StartUp.Registry;
using App.Utility;
using CSharp_Result;
using Domain.Exceptions;

namespace App.Modules.Payments.Airwallex;

public class AirWallexClient(
  IHttpClientFactory factory,
  IGatewayAuthenticator authenticator,
  ILogger<AirWallexClient> logger
)
{
  private HttpClient HttpClient => factory.CreateClient(HttpClients.Airwallex);

  public Task<Result<AirwallexCreateIntentRes>> CreateIntent(AirwallexCreateIntentReq req)
  {
    return authenticator
      .GetToken()
      .ThenAwait(async token =>
      {
        var request = new HttpRequestMessage
        {
          Method = HttpMethod.Post,
          RequestUri = new Uri("api/v1/pa/payment_intents/create", UriKind.Relative),
          Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
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
          logger.LogError(
            e,
            "Failed to authenticate with Airwallex (HTTP Error), Response: {Body}",
            body
          );
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

  public Task<Result<AirwallexTransferRes>> CreateTransfer(AirwallexCreateTransferReq req)
  {
    return authenticator
      .GetToken()
      .ThenAwait(async token =>
      {
        var request = new HttpRequestMessage
        {
          Method = HttpMethod.Post,
          RequestUri = new Uri("api/v1/transfers/create", UriKind.Relative),
          Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
          Content = JsonContent.Create(req),
        };
        // every failure below is returned as a failed Result, never thrown:
        // Approve's compensation logic must be able to classify it
        try
        {
          using var response = await this.HttpClient.SendAsync(request);
          var body = await response.Content.ReadAsStringAsync();
          if (response.IsSuccessStatusCode)
            return body.ToObj<AirwallexTransferRes>().ToResult();

          logger.LogError(
            "Failed to create transfer with Airwallex, Status: {Status}, Response: {Body}",
            (int)response.StatusCode,
            body
          );
          // a definitive validation rejection proves no transfer was created;
          // 408/429 give no such proof and stay ambiguous, like 5xx/network
          var status = (int)response.StatusCode;
          if (status is >= 400 and < 500 and not 408 and not 429)
            return (Result<AirwallexTransferRes>)
              new PayoutRejectedException($"Airwallex rejected the transfer ({status}): {body}");
          return (Result<AirwallexTransferRes>)
            new HttpRequestException($"Airwallex transfer creation failed ({status}): {body}");
        }
        catch (Exception e)
        {
          // network fault / timeout: the transfer may or may not exist —
          // ambiguous by definition
          logger.LogError(e, "Failed to create transfer with Airwallex (transport error)");
          return (Result<AirwallexTransferRes>)e;
        }
      });
  }

  // Point-in-time lookup for reconciliation. Returns null (not an error) when
  // the gateway definitively has no such transfer.
  public Task<Result<AirwallexTransferRes?>> GetTransfer(string transferId)
  {
    return this.LookupTransfer($"api/v1/transfers/{transferId}", isList: false);
  }

  public Task<Result<AirwallexTransferRes?>> GetTransferByRequestId(string requestId)
  {
    return this.LookupTransfer(
      $"api/v1/transfers?request_id={Uri.EscapeDataString(requestId)}",
      isList: true
    );
  }

  private Task<Result<AirwallexTransferRes?>> LookupTransfer(string path, bool isList)
  {
    return authenticator
      .GetToken()
      .ThenAwait(async token =>
      {
        var request = new HttpRequestMessage
        {
          Method = HttpMethod.Get,
          RequestUri = new Uri(path, UriKind.Relative),
          Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };
        try
        {
          using var response = await this.HttpClient.SendAsync(request);
          var body = await response.Content.ReadAsStringAsync();
          if ((int)response.StatusCode == 404)
            return (Result<AirwallexTransferRes?>)(AirwallexTransferRes?)null;
          if (!response.IsSuccessStatusCode)
          {
            logger.LogError(
              "Failed to look up Airwallex transfer, Status: {Status}, Response: {Body}",
              (int)response.StatusCode,
              body
            );
            return (Result<AirwallexTransferRes?>)
              new HttpRequestException(
                $"Airwallex transfer lookup failed ({(int)response.StatusCode}): {body}"
              );
          }
          if (!isList)
            return body.ToObj<AirwallexTransferRes>().ToResult().Then(
              t => (AirwallexTransferRes?)t,
              Errors.MapNone
            );
          var list = body.ToObj<AirwallexTransferListRes>();
          return (Result<AirwallexTransferRes?>)(
            list.Items is { Length: > 0 } ? list.Items[0] : null
          );
        }
        catch (Exception e)
        {
          logger.LogError(e, "Failed to look up Airwallex transfer (transport error)");
          return (Result<AirwallexTransferRes?>)e;
        }
      });
  }
}
