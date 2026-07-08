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

// Payouts (Transfers) API — PayNow payout is an Airwallex Beta feature that
// must be enabled by the account manager; the beneficiary shape below follows
// the documented SG payout network (PayNow ID via account_routing_type1)
public record AirwallexCreateTransferReq
{
  [JsonPropertyName("request_id")]
  public string RequestId { get; set; } = null!;

  [JsonPropertyName("source_currency")]
  public string SourceCurrency { get; set; } = null!;

  [JsonPropertyName("transfer_currency")]
  public string TransferCurrency { get; set; } = null!;

  [JsonPropertyName("transfer_amount")]
  public decimal TransferAmount { get; set; }

  [JsonPropertyName("transfer_method")]
  public string TransferMethod { get; set; } = null!;

  [JsonPropertyName("reason")]
  public string Reason { get; set; } = null!;

  [JsonPropertyName("reference")]
  public string Reference { get; set; } = null!;

  [JsonPropertyName("beneficiary")]
  public AirwallexTransferBeneficiary Beneficiary { get; set; } = null!;
}

public record AirwallexTransferBeneficiary
{
  [JsonPropertyName("entity_type")]
  public string EntityType { get; set; } = null!;

  [JsonPropertyName("bank_details")]
  public AirwallexTransferBankDetails BankDetails { get; set; } = null!;
}

public record AirwallexTransferBankDetails
{
  [JsonPropertyName("account_currency")]
  public string AccountCurrency { get; set; } = null!;

  [JsonPropertyName("bank_country_code")]
  public string BankCountryCode { get; set; } = null!;

  [JsonPropertyName("account_routing_type1")]
  public string AccountRoutingType1 { get; set; } = null!;

  [JsonPropertyName("account_routing_value1")]
  public string AccountRoutingValue1 { get; set; } = null!;
}
