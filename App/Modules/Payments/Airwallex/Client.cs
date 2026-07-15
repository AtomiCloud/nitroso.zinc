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

  public Task<Result<AirwallexRefundRes>> CreateRefund(AirwallexCreateRefundReq req)
  {
    return authenticator
      .GetToken()
      .ThenAwait(async token =>
      {
        var request = new HttpRequestMessage
        {
          Method = HttpMethod.Post,
          RequestUri = new Uri("api/v1/pa/refunds/create", UriKind.Relative),
          Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
          Content = JsonContent.Create(req),
        };
        // like CreateTransfer: every failure is a failed Result, never a
        // throw — the approve flow's compensation logic must classify it.
        // Unlike transfers, refunds are never bounced back to Pending on a
        // 4xx: sibling fragments may already exist at the gateway, so ALL
        // failures are treated as ambiguous by the caller.
        try
        {
          using var response = await this.HttpClient.SendAsync(request);
          var body = await response.Content.ReadAsStringAsync();
          if (response.IsSuccessStatusCode)
            return body.ToObj<AirwallexRefundRes>().ToResult();

          logger.LogError(
            "Failed to create refund with Airwallex, Status: {Status}, Response: {Body}",
            (int)response.StatusCode,
            body
          );
          return (Result<AirwallexRefundRes>)
            new HttpRequestException(
              $"Airwallex refund creation failed ({(int)response.StatusCode}): {body}"
            );
        }
        catch (Exception e)
        {
          logger.LogError(e, "Failed to create refund with Airwallex (transport error)");
          return (Result<AirwallexRefundRes>)e;
        }
      });
  }

  // Point-in-time refund lookup for reconciliation. Returns null (not an
  // error) when the gateway definitively has no such refund.
  public Task<Result<AirwallexRefundRes?>> GetRefund(string refundId)
  {
    return authenticator
      .GetToken()
      .ThenAwait(async token =>
      {
        var request = new HttpRequestMessage
        {
          Method = HttpMethod.Get,
          RequestUri = new Uri($"api/v1/pa/refunds/{refundId}", UriKind.Relative),
          Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };
        try
        {
          using var response = await this.HttpClient.SendAsync(request);
          var body = await response.Content.ReadAsStringAsync();
          if ((int)response.StatusCode == 404)
            return (Result<AirwallexRefundRes?>)(AirwallexRefundRes?)null;
          if (!response.IsSuccessStatusCode)
          {
            logger.LogError(
              "Failed to look up Airwallex refund, Status: {Status}, Response: {Body}",
              (int)response.StatusCode,
              body
            );
            return (Result<AirwallexRefundRes?>)
              new HttpRequestException(
                $"Airwallex refund lookup failed ({(int)response.StatusCode}): {body}"
              );
          }
          return body.ToObj<AirwallexRefundRes>()
            .ToResult()
            .Then(r => (AirwallexRefundRes?)r, Errors.MapNone);
        }
        catch (Exception e)
        {
          logger.LogError(e, "Failed to look up Airwallex refund (transport error)");
          return (Result<AirwallexRefundRes?>)e;
        }
      });
  }

  // Point-in-time intent lookup: the gateway keys a payment's financial
  // transactions by the payment ATTEMPT id, so fee capture resolves the
  // intent's latest_payment_attempt through this. Returns null (not an
  // error) when the gateway definitively has no such intent.
  public Task<Result<AirwallexPaymentIntentRes?>> GetPaymentIntent(string intentId)
  {
    return authenticator
      .GetToken()
      .ThenAwait(async token =>
      {
        var request = new HttpRequestMessage
        {
          Method = HttpMethod.Get,
          RequestUri = new Uri(
            $"api/v1/pa/payment_intents/{Uri.EscapeDataString(intentId)}",
            UriKind.Relative
          ),
          Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
        };
        try
        {
          using var response = await this.HttpClient.SendAsync(request);
          var body = await response.Content.ReadAsStringAsync();
          if ((int)response.StatusCode == 404)
            return (Result<AirwallexPaymentIntentRes?>)(AirwallexPaymentIntentRes?)null;
          if (!response.IsSuccessStatusCode)
          {
            logger.LogError(
              "Failed to look up Airwallex payment intent, Status: {Status}, Response: {Body}",
              (int)response.StatusCode,
              body
            );
            return (Result<AirwallexPaymentIntentRes?>)
              new HttpRequestException(
                $"Airwallex payment intent lookup failed ({(int)response.StatusCode}): {body}"
              );
          }
          return body.ToObj<AirwallexPaymentIntentRes>()
            .ToResult()
            .Then(i => (AirwallexPaymentIntentRes?)i, Errors.MapNone);
        }
        catch (Exception e)
        {
          logger.LogError(e, "Failed to look up Airwallex payment intent (transport error)");
          return (Result<AirwallexPaymentIntentRes?>)e;
        }
      });
  }

  // The gateway's own fee ledger for one money movement: every financial
  // transaction reported against source_id (an intent, transfer or refund
  // id), following page_num until has_more is false. An empty list is a
  // normal answer — fees post with delay.
  public Task<Result<AirwallexFinancialTransactionRes[]>> ListFinancialTransactionsBySource(
    string sourceId
  )
  {
    return authenticator
      .GetToken()
      .ThenAwait(async token =>
      {
        try
        {
          var items = new List<AirwallexFinancialTransactionRes>();
          var page = 0;
          while (true)
          {
            var request = new HttpRequestMessage
            {
              Method = HttpMethod.Get,
              RequestUri = new Uri(
                $"api/v1/financial_transactions?source_id={Uri.EscapeDataString(sourceId)}"
                  + $"&page_num={page}&page_size=100",
                UriKind.Relative
              ),
              Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            };
            using var response = await this.HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            // a 404 is "no rows for this source (yet)" — a normal empty answer
            if ((int)response.StatusCode == 404)
              return (Result<AirwallexFinancialTransactionRes[]>)items.ToArray();
            if (!response.IsSuccessStatusCode)
            {
              logger.LogError(
                "Failed to list Airwallex financial transactions for '{SourceId}', "
                  + "Status: {Status}, Response: {Body}",
                sourceId,
                (int)response.StatusCode,
                body
              );
              return (Result<AirwallexFinancialTransactionRes[]>)
                new HttpRequestException(
                  $"Airwallex financial transaction listing failed ({(int)response.StatusCode}): {body}"
                );
            }

            var list = body.ToObj<AirwallexFinancialTransactionListRes>();
            items.AddRange(list.Items ?? []);
            if (!list.HasMore || list.Items is not { Length: > 0 })
              return (Result<AirwallexFinancialTransactionRes[]>)items.ToArray();
            page++;
          }
        }
        catch (Exception e)
        {
          logger.LogError(
            e,
            "Failed to list Airwallex financial transactions for '{SourceId}' (transport error)",
            sourceId
          );
          return (Result<AirwallexFinancialTransactionRes[]>)e;
        }
      });
  }

  // The gateway's ledger filtered by transaction type over a created-at
  // window: every financial transaction of the given type (e.g. FEE — the
  // account-level fee billings that own no movement we track) created in
  // [fromUtc, toUtc), following page_num until has_more is false. Same
  // endpoint and paging as the source_id listing above; from_created_at /
  // to_created_at are the API's documented window params.
  public Task<Result<AirwallexFinancialTransactionRes[]>> ListFinancialTransactionsByType(
    string transactionType,
    DateTime fromUtc,
    DateTime toUtc
  )
  {
    return authenticator
      .GetToken()
      .ThenAwait(async token =>
      {
        try
        {
          var from = Uri.EscapeDataString(fromUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"));
          var to = Uri.EscapeDataString(toUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"));
          var items = new List<AirwallexFinancialTransactionRes>();
          var page = 0;
          while (true)
          {
            var request = new HttpRequestMessage
            {
              Method = HttpMethod.Get,
              RequestUri = new Uri(
                $"api/v1/financial_transactions?transaction_type={Uri.EscapeDataString(transactionType)}"
                  + $"&from_created_at={from}&to_created_at={to}"
                  + $"&page_num={page}&page_size=100",
                UriKind.Relative
              ),
              Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            };
            using var response = await this.HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            // a 404 is "no rows in this window" — a normal empty answer
            if ((int)response.StatusCode == 404)
              return (Result<AirwallexFinancialTransactionRes[]>)items.ToArray();
            if (!response.IsSuccessStatusCode)
            {
              logger.LogError(
                "Failed to list Airwallex financial transactions of type '{Type}', "
                  + "Status: {Status}, Response: {Body}",
                transactionType,
                (int)response.StatusCode,
                body
              );
              return (Result<AirwallexFinancialTransactionRes[]>)
                new HttpRequestException(
                  $"Airwallex financial transaction listing failed ({(int)response.StatusCode}): {body}"
                );
            }

            var list = body.ToObj<AirwallexFinancialTransactionListRes>();
            items.AddRange(list.Items ?? []);
            if (!list.HasMore || list.Items is not { Length: > 0 })
              return (Result<AirwallexFinancialTransactionRes[]>)items.ToArray();
            page++;
          }
        }
        catch (Exception e)
        {
          logger.LogError(
            e,
            "Failed to list Airwallex financial transactions of type '{Type}' (transport error)",
            transactionType
          );
          return (Result<AirwallexFinancialTransactionRes[]>)e;
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
