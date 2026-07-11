using System.ComponentModel;
using System.Text.Json.Serialization;

namespace App.Error.V1;

[Description(
  "The user's refundable pool (captured card payments inside the refund window, minus refunds already issued) cannot cover the net amount of the card-refund withdrawal"
)]
public class InsufficientRefundablePool : IDomainProblem
{
  public InsufficientRefundablePool() { }

  public InsufficientRefundablePool(string detail, decimal required, decimal available)
  {
    this.Detail = detail;
    this.Required = required;
    this.Available = available;
  }

  [JsonIgnore]
  public string Id { get; } = "insufficient_refundable_pool";

  [JsonIgnore]
  public string Title { get; } = "Insufficient Refundable Pool";

  [JsonIgnore]
  public string Version { get; } = "v1";

  public string Detail { get; } = string.Empty;

  [Description("The net amount in SGD the card refunds must cover (gross minus fee)")]
  public decimal Required { get; }

  [Description("The amount in SGD the refundable pool can still cover")]
  public decimal Available { get; }
}
