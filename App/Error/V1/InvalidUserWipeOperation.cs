using System.ComponentModel;
using System.Text.Json.Serialization;

namespace App.Error.V1;

[Description(
  "The PDPA account wipe attempted is not valid: the wallet still holds money, a withdrawal is still in flight, or the user is already wiped"
)]
public class InvalidUserWipeOperation : IDomainProblem
{
  public InvalidUserWipeOperation() { }

  public InvalidUserWipeOperation(string detail, string userId, string reason)
  {
    this.Detail = detail;
    this.UserId = userId;
    this.Reason = reason;
  }

  [JsonIgnore]
  public string Id { get; } = "invalid_user_wipe_operation";

  [JsonIgnore]
  public string Title { get; } = "Invalid User Wipe Operation";

  [JsonIgnore]
  public string Version { get; } = "v1";

  public string Detail { get; } = string.Empty;

  [Description("The user targeted by the wipe")]
  public string UserId { get; } = string.Empty;

  [Description(
    "Why the wipe was refused: wallet_not_empty, withdrawal_in_flight or already_wiped"
  )]
  public string Reason { get; } = string.Empty;
}
