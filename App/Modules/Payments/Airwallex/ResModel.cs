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

// One Airwallex "financial transaction" — the gateway's own money-movement
// ledger, including its fee take. Field names follow the public
// financial_transactions API (id, source_id, transaction_type, amount, fee,
// net, currency, created_at); the record deliberately tolerates any extra
// fields the gateway adds, and every non-id field is defaulted so a missing
// field never throws during deserialization.
public record AirwallexFinancialTransactionRes
{
  [JsonPropertyName("id")]
  public string Id { get; set; } = null!;

  [JsonPropertyName("source_id")]
  public string? SourceId { get; set; }

  [JsonPropertyName("transaction_type")]
  public string? TransactionType { get; set; }

  [JsonPropertyName("status")]
  public string? Status { get; set; }

  [JsonPropertyName("amount")]
  public decimal Amount { get; set; }

  [JsonPropertyName("fee")]
  public decimal Fee { get; set; }

  [JsonPropertyName("net")]
  public decimal Net { get; set; }

  [JsonPropertyName("currency")]
  public string Currency { get; set; } = string.Empty;

  [JsonPropertyName("created_at")]
  public DateTime CreatedAt { get; set; }
}

public record AirwallexFinancialTransactionListRes
{
  [JsonPropertyName("has_more")]
  public bool HasMore { get; set; }

  [JsonPropertyName("items")]
  public AirwallexFinancialTransactionRes[]? Items { get; set; }
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
