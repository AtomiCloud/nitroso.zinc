using System.Text.Json.Serialization;

namespace App.Modules.Payments.Airwallex;

public record AirwallexAuthTokenRes
{
  [JsonPropertyName("expires_at")]
  public string ExpiresAt { get; set; } = null!;

  [JsonPropertyName("token")]
  public string Token { get; set; } = null!;
}

public record AirwallexCreateIntentRes
{
  [JsonPropertyName("id")]
  public string Id { get; set; } = null!;

  [JsonPropertyName("request_id")]
  public Guid RequestId { get; set; }

  [JsonPropertyName("amount")]
  public decimal Amount { get; set; }

  [JsonPropertyName("currency")]
  public string Currency { get; set; } = null!;

  [JsonPropertyName("merchant_order_id")]
  public Guid MerchantOrderId { get; set; }

  [JsonPropertyName("descriptor")]
  public string Descriptor { get; set; } = null!;

  [JsonPropertyName("status")]
  public string Status { get; set; } = null!;

  [JsonPropertyName("captured_amount")]
  public decimal CapturedAmount { get; set; }

  [JsonPropertyName("created_at")]
  public string CreatedAt { get; set; } = null!;

  [JsonPropertyName("updated_at")]
  public string UpdatedAt { get; set; } = null!;

  [JsonPropertyName("available_payment_method_types")]
  public string[] AvailablePaymentMethodTypes { get; set; } = null!;

  [JsonPropertyName("client_secret")]
  public string ClientSecret { get; set; } = null!;

  [JsonPropertyName("base_amount")]
  public decimal BaseAmount { get; set; }

  [JsonPropertyName("base_currency")]
  public string BaseCurrency { get; set; } = null!;
}

public record AirwallexTransferRes
{
  [JsonPropertyName("id")]
  public string Id { get; set; } = null!;

  [JsonPropertyName("request_id")]
  public string RequestId { get; set; } = null!;

  [JsonPropertyName("status")]
  public string Status { get; set; } = null!;

  [JsonPropertyName("short_reference_id")]
  public string? ShortReferenceId { get; set; }
}

public record AirwallexTransferListRes
{
  [JsonPropertyName("items")]
  public AirwallexTransferRes[]? Items { get; set; }
}

// Shared status classification for transfer objects, used by both the webhook
// adapter and the reconciliation lookup so the two paths can never disagree
public static class AirwallexTransferStatuses
{
  public static readonly string[] Settled = ["PAID", "SETTLED"];

  public static readonly string[] Failed = ["FAILED", "CANCELLED", "REJECTED", "RETURNED"];
}

public record AirwallexRefundRes
{
  [JsonPropertyName("id")]
  public string Id { get; set; } = null!;

  [JsonPropertyName("request_id")]
  public string RequestId { get; set; } = null!;

  [JsonPropertyName("payment_intent_id")]
  public string PaymentIntentId { get; set; } = null!;

  [JsonPropertyName("amount")]
  public decimal Amount { get; set; }

  [JsonPropertyName("status")]
  public string Status { get; set; } = null!;
}

// Shared status classification for refund objects (webhook adapter and
// reconciliation lookup). Refund events are refund.received / accepted /
// settled / failed — SETTLED is the only terminal success, FAILED the only
// terminal failure; RECEIVED / ACCEPTED are in flight.
public static class AirwallexRefundStatuses
{
  public static readonly string[] Settled = ["SETTLED", "SUCCEEDED"];

  public static readonly string[] Failed = ["FAILED", "CANCELLED"];
}
