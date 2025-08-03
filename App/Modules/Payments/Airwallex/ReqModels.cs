using System.Text.Json.Serialization;

namespace App.Modules.Payments.Airwallex;

public record AirwallexCreateIntentReq
{
  [JsonPropertyName("request_id")]
  public Guid RequestId { get; set; }

  [JsonPropertyName("amount")]
  public decimal Amount { get; set; }

  [JsonPropertyName("currency")]
  public string Currency { get; set; } = null!;

  [JsonPropertyName("merchant_order_id")]
  public Guid MerchantOrderId { get; set; }
}
