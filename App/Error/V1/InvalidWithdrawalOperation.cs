using System.ComponentModel;
using System.Text.Json.Serialization;
using App.Modules.Withdrawals.API.V1;
using Domain.Withdrawal;

namespace App.Error.V1;

[Description(
  "The withdrawal operation attempted (Approve, Complete, Cancel, Reject etc) is not valid for the current withdrawal state"
)]
public class InvalidWithdrawalOperation : IDomainProblem
{
  public InvalidWithdrawalOperation() { }

  public InvalidWithdrawalOperation(string detail, WithdrawStatus withdrawStatus, string operation)
  {
    this.Detail = detail;
    this.WithdrawStatus = withdrawStatus.ToRes();
    this.Operation = operation;
  }

  [JsonIgnore]
  public string Id { get; } = "invalid_withdrawal_operation";

  [JsonIgnore]
  public string Title { get; } = "Invalid Withdrawal Operation";

  [JsonIgnore]
  public string Version { get; } = "v1";

  public string Detail { get; } = string.Empty;

  [Description(
    "The current status of the withdrawal that was invalid for the operation attempted"
  )]
  public string WithdrawStatus { get; } = string.Empty;

  [Description("The operation that was invalid")]
  public string Operation { get; } = string.Empty;
}
