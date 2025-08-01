using System.ComponentModel;
using System.Text.Json.Serialization;
using App.Modules.Common;

namespace App.Error.V1;

[Description(
  "The account being transacted will overdraft if this transaction is processed - the account does not have sufficient balance"
)]
public class InsufficientBalance : IDomainProblem
{
  public InsufficientBalance() { }

  public InsufficientBalance(
    string detail,
    string userId,
    Guid walletId,
    decimal amount,
    string account
  )
  {
    this.Detail = detail;
    this.UserId = userId;
    this.WalletId = walletId;
    this.Amount = amount;
    this.Account = account;
  }

  [JsonIgnore]
  public string Id { get; } = "insufficient_balance";

  [JsonIgnore]
  public string Title { get; } = "Insufficient Balance";

  [JsonIgnore]
  public string Version { get; } = "v1";

  public string Detail { get; } = string.Empty;

  [Description("The ID of the user that the account with insufficient balance belongs to")]
  public string UserId { get; } = string.Empty;

  [Description("The ID of the wallet that the account with insufficient balance belongs to")]
  public Guid WalletId { get; } = Guid.Empty;

  [Description("The Amount in SGD attempted to be transacted from the account")]
  public decimal Amount { get; } = 0;

  [Description("The account that has insufficient balance")]
  public string Account { get; } = "";
}
