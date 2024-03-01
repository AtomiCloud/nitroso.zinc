using System.Text.Json;
using App.Modules.Payments.Airwallex;
using CSharp_Result;
using Domain.Payment;

namespace App.Modules.Payments.Data;

public class AirwallexGateway(
  AirWallexClient client
  ) : IPaymentGateway
{

  private const string Gateway = "Airwallex";
  public async Task<Result<(PaymentReference, PaymentRecord, PaymentSecret)>> Create(Guid id, decimal amount, string currency)
  {
    var req = new AirwallexCreateIntentReq
    {
      RequestId = id,
      Amount = amount,
      Currency = currency,
      MerchantOrderId = id,
    };
    return await client.CreateIntent(req)
      .Then(res =>
      {
        var reference = new PaymentReference
        {
          Id = id,
          ExternalReference = res.Id,
          Gateway = Gateway,
        };

        var record = new PaymentRecord
        {
          Amount = res.Amount,
          CapturedAmount = res.CapturedAmount,
          Currency = res.Currency,
          LastUpdated = DateTime.UtcNow,
          Status = res.Status,
          AdditionalData = JsonDocument.Parse("{}"),
        };

        var secret = new PaymentSecret
        {
          Secret = res.ClientSecret,
        };
        return (reference, record, secret);

      }, Errors.MapNone);
  }
}
