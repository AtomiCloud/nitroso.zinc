using System.ComponentModel;
using System.Text.Json.Serialization;

namespace App.Error.V1;

[Description(
  "The booking has used up its recovery budget (recycled from 'Recovering' back to 'Pending' the maximum number of times, past its retry window since the first duplicate-passport rejection, or too close to departure to be bought again); it stays in 'Recovering' and must be resolved (e.g. marked Duplicate or moved to manual intervention)"
)]
public class RecoveryRetriesExhausted : IDomainProblem
{
  public RecoveryRetriesExhausted() { }

  public RecoveryRetriesExhausted(
    string detail,
    string bookingId,
    int retries,
    int maxRetries,
    string reason
  )
  {
    this.Detail = detail;
    this.BookingId = bookingId;
    this.Retries = retries;
    this.MaxRetries = maxRetries;
    this.Reason = reason;
  }

  [JsonIgnore]
  public string Id { get; } = "recovery_retries_exhausted";

  [JsonIgnore]
  public string Title { get; } = "Recovery Retries Exhausted";

  [JsonIgnore]
  public string Version { get; } = "v1";

  public string Detail { get; } = string.Empty;

  [Description("The booking whose recovery retries are exhausted")]
  public string BookingId { get; } = string.Empty;

  [Description("How many recovery retries the booking has consumed")]
  public int Retries { get; }

  [Description("The configured maximum number of recovery retries")]
  public int MaxRetries { get; }

  [Description(
    "Which budget ran out: Retries (the cap), Window (time since the first duplicate-passport rejection) or Departure (too close to departure to buy again)"
  )]
  public string Reason { get; } = string.Empty;
}
