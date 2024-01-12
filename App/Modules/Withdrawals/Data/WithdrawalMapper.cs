using App.Modules.Users.Data;
using App.Modules.Wallets.Data;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.Data;

public static class WithdrawalMapper
{
  // Data -> Domain
  public static WithdrawalRecord ToRecord(this WithdrawalData data) => new()
  {
    Amount = data.Amount,
    PayNowNumber = data.PayNowNumber,
  };

  public static WithdrawalStatus ToStatus(this WithdrawalData data) => new() { Status = (WithdrawStatus)data.Status };

  public static WithdrawalComplete? ToComplete(this WithdrawalData data)
  {
    if (data.CompletedAt == null) return null;
    return new WithdrawalComplete
    {
      CompletedAt = data.CompletedAt.Value,
      CompleterId = data.CompleterId ?? "",
      Note = data.Note ?? "",
      Receipt = data.Receipt,
    };
  }

  public static WithdrawalPrincipal ToPrincipal(this WithdrawalData data) => new()
  {
    Id = data.Id,
    Record = data.ToRecord(),
    Complete = data.ToComplete(),
    Status = data.ToStatus(),
    CreatedAt = data.CreatedAt,
  };


  public static Withdrawal ToDomain(this WithdrawalData data) => new()
  {
    Principal = data.ToPrincipal(),
    Wallet = data.Wallet.ToPrincipal(),
    User = data.Wallet.User.ToPrincipal(),
    Completer = data.Completer?.ToPrincipal(),
  };

  // Domain -> Data 
  public static WithdrawalData Update(this WithdrawalData data, WithdrawalRecord record)
  {
    data.Amount = record.Amount;
    data.PayNowNumber = record.PayNowNumber;
    return data;
  }

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

}
