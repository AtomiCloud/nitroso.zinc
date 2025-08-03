using App.Modules.Users.API.V1;
using App.Modules.Wallets.API.V1;
using App.Utility;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.API.V1;

public static class WithdrawalMapper
{
  // Domain -> RES
  public static string ToRes(this WithdrawStatus status) =>
    status switch
    {
      WithdrawStatus.Pending => "Pending",
      WithdrawStatus.Completed => "Completed",
      WithdrawStatus.Rejected => "Rejected",
      WithdrawStatus.Cancel => "Cancel",
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static WithdrawalStatusRes ToRes(this WithdrawalStatus status) =>
    new(status.Status.ToRes());

  public static WithdrawalCompleteRes ToRes(this WithdrawalComplete complete) =>
    new(complete.CompletedAt, complete.Note, complete.Receipt);

  public static WithdrawalRecordRes ToRes(this WithdrawalRecord record) =>
    new(record.Amount, record.PayNowNumber);

  public static WithdrawalPrincipalRes ToRes(this WithdrawalPrincipal w) =>
    new(w.Id, w.CreatedAt, w.Status.ToRes(), w.Record.ToRes(), w.Complete?.ToRes());

  public static WithdrawalRes ToRes(this Withdrawal w) =>
    new(w.Principal.ToRes(), w.User.ToRes(), w.Completer?.ToRes(), w.Wallet.ToRes());

  // REQ -> Domain
  public static WithdrawStatus ToWithdrawStatus(this string status) =>
    status switch
    {
      "Pending" => WithdrawStatus.Pending,
      "Completed" => WithdrawStatus.Completed,
      "Rejected" => WithdrawStatus.Rejected,
      "Cancel" => WithdrawStatus.Cancel,
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static WithdrawalRecord ToDomain(this CreateWithdrawalReq record) =>
    new() { Amount = record.Amount, PayNowNumber = record.PayNowNumber };

  public static WithdrawalSearch ToDomain(this SearchWithdrawalQuery query) =>
    new()
    {
      Id = query.Id,
      UserId = query.UserId,
      CompleterId = query.CompleterId,
      Min = query.Min,
      Max = query.Max,
      Status = query.Status?.ToWithdrawStatus(),
      Before = query.Before?.ToDate(),
      After = query.After?.ToDate(),
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };
}
