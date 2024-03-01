using System.Text.Json.Serialization;

namespace App.Modules.Payments.Airwallex;

public record AirwallexEvent
{
  [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

  [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

  [JsonPropertyName("account_id")] public string AirwallexEventAccountId { get; set; } = string.Empty;

  [JsonPropertyName("accountId")] public string AccountId { get; set; } = string.Empty;

  [JsonPropertyName("data")] public AirwallexEventData Data { get; set; } = new();

  [JsonPropertyName("created_at")] public string AirwallexEventCreatedAt { get; set; } = string.Empty;

  [JsonPropertyName("createdAt")] public string CreatedAt { get; set; } = string.Empty;

  [JsonPropertyName("sourceId")] public string SourceId { get; set; } = string.Empty;
}

public record AirwallexEventData
{
  [JsonPropertyName("object")] public AirwallexEventDataObject Object { get; set; } = new();
}

public record AirwallexEventDataObject
{
  [JsonPropertyName("amount")] public decimal Amount { get; set; }

  [JsonPropertyName("base_amount")] public decimal BaseAmount { get; set; }

  [JsonPropertyName("base_currency")] public string BaseCurrency { get; set; } = string.Empty;

  [JsonPropertyName("captured_amount")] public decimal CapturedAmount { get; set; }

  [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = string.Empty;

  [JsonPropertyName("currency")] public string Currency { get; set; } = string.Empty;

  [JsonPropertyName("descriptor")] public string Descriptor { get; set; } = string.Empty;

  [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

  [JsonPropertyName("merchant_order_id")]
  public Guid MerchantOrderId { get; set; }

  [JsonPropertyName("request_id")] public Guid RequestId { get; set; }

  [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;

  [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = string.Empty;
}
