using System.ComponentModel;
using System.Text.Json.Serialization;

namespace App.Error.V1;

[Description(
  "Airwallex could not be asked which refunds already exist against the user's card payments, so the refundable pool cannot be computed safely right now"
)]
public class RefundGatewayUnavailable : IDomainProblem
{
  public RefundGatewayUnavailable() { }

  public RefundGatewayUnavailable(string detail)
  {
    this.Detail = detail;
  }

  [JsonIgnore]
  public string Id { get; } = "refund_gateway_unavailable";

  [JsonIgnore]
  public string Title { get; } = "Refund Gateway Unavailable";

  [JsonIgnore]
  public string Version { get; } = "v1";

  public string Detail { get; } = string.Empty;
}
