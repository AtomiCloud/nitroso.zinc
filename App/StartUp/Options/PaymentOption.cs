using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class PaymentOption
{
  public const string Key = "Payment";

  public AirwallexOption Airwallex { get; set; } = new();
}

public class AirwallexOption
{
  [Required, Url]
  public string BaseAddress { get; set; } = string.Empty;

  [Required, MinLength(1)]
  public string ClientId { get; set; } = string.Empty;

  [Required, MinLength(1)]
  public string ApiKey { get; set; } = string.Empty;

  [Required, MaxLength(1)]
  public string WebhookKey { get; set; } = string.Empty;
}
