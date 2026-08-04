using App.Modules.Users.Data;
using App.Modules.Wallets.Data;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.Data;

public static class WithdrawalMapper
{
  // Data -> Domain
  public static WithdrawalRecord ToRecord(this WithdrawalData data) =>
    new()
    {
      Amount = data.Amount,
      Method = (WithdrawalMethod)data.Method,
      // card refunds carry no PayNow id; the column stores empty string
      PayNowNumber = string.IsNullOrEmpty(data.PayNowNumber) ? null : data.PayNowNumber,
    };

  public static WithdrawalStatus ToStatus(this WithdrawalData data) =>
    new() { Status = (WithdrawStatus)data.Status };

  public static WithdrawalComplete? ToComplete(this WithdrawalData data)
  {
    if (data.CompletedAt == null)
      return null;
    return new WithdrawalComplete
    {
      CompletedAt = data.CompletedAt.Value,
      CompleterId = data.CompleterId,
      Note = data.Note ?? "",
      Receipt = data.Receipt,
    };
  }

  public static WithdrawalPayout? ToPayout(this WithdrawalData data)
  {
    if (data.Fee == null)
      return null;
    return new WithdrawalPayout
    {
      ConfirmationNumber = data.ConfirmationNumber,
      Fee = data.Fee.Value,
      Attempt = data.PayoutAttempt,
      ReconcileAttempts = data.ReconcileAttempts,
    };
  }

  public static WithdrawalPrincipal ToPrincipal(this WithdrawalData data) =>
    new()
    {
      Id = data.Id,
      Record = data.ToRecord(),
      Complete = data.ToComplete(),
      Status = data.ToStatus(),
      Payout = data.ToPayout(),
      CreatedAt = data.CreatedAt,
    };

  public static Withdrawal ToDomain(this WithdrawalData data) =>
    new()
    {
      Principal = data.ToPrincipal(),
      Wallet = data.Wallet.ToPrincipal(),
      User = data.Wallet.User.ToPrincipal(),
      Completer = data.Completer?.ToPrincipal(),
      Refunds = data
        .Refunds.OrderBy(x => x.RequestId, StringComparer.Ordinal)
        .Select(x => x.ToDomain())
        .ToList(),
    };

  // Domain -> Data
  public static WithdrawalData Update(this WithdrawalData data, WithdrawalRecord record)
  {
    data.Amount = record.Amount;
    data.Method = (byte)record.Method;
    data.PayNowNumber = record.PayNowNumber ?? string.Empty;
    return data;
  }

  // Refund fragments (evidence rows)
  public static WithdrawalRefundFragment ToDomain(this WithdrawalRefundData data) =>
    new()
    {
      Id = data.Id,
      WithdrawalId = data.WithdrawalId,
      PaymentId = data.PaymentId,
      PaymentIntentId = data.PaymentIntentId,
      AirwallexRefundId = data.AirwallexRefundId,
      AcquirerReferenceNumber = data.AcquirerReferenceNumber,
      RequestId = data.RequestId,
      Amount = data.Amount,
      Status = (RefundFragmentStatus)data.Status,
      CreatedAt = data.CreatedAt,
      SettledAt = data.SettledAt,
    };

  public static WithdrawalRefundData ToData(this WithdrawalRefundFragment fragment) =>
    new()
    {
      Id = fragment.Id,
      WithdrawalId = fragment.WithdrawalId,
      PaymentId = fragment.PaymentId,
      PaymentIntentId = fragment.PaymentIntentId,
      AirwallexRefundId = fragment.AirwallexRefundId,
      AcquirerReferenceNumber = fragment.AcquirerReferenceNumber,
      RequestId = fragment.RequestId,
      Amount = fragment.Amount,
      Status = (byte)fragment.Status,
      CreatedAt = fragment.CreatedAt,
      SettledAt = fragment.SettledAt,
    };

  public static WithdrawalData Update(this WithdrawalData data, WithdrawalStatus record)
  {
    data.Status = (byte)record.Status;
    return data;
  }

  public static WithdrawalData Update(this WithdrawalData data, WithdrawalComplete record)
  {
    data.CompletedAt = record.CompletedAt;
    data.CompleterId = record.CompleterId;
    data.Note = record.Note;
    data.Receipt = record.Receipt;
    return data;
  }

  public static WithdrawalData Update(this WithdrawalData data, WithdrawalPayout record)
  {
    data.ConfirmationNumber = record.ConfirmationNumber;
    data.Fee = record.Fee;
    data.PayoutAttempt = record.Attempt;
    data.ReconcileAttempts = record.ReconcileAttempts;
    return data;
  }
}
