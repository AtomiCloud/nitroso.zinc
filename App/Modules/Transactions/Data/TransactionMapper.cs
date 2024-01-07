using App.Modules.Users.Data;
using App.Modules.Wallets.Data;
using Domain.Transaction;

namespace App.Modules.Transactions.Data;

public static class TransactionMapper
{
  // Data -> Domain
  public static TransactionRecord ToRecord(this TransactionData principal) => new()
  {
    Name = principal.Name,
    Description = principal.Description,
    Type = (TransactionType)principal.TransactionType,
    Amount = principal.Amount,
    From = principal.From,
    To = principal.To,
  };

  public static TransactionPrincipal ToPrincipal(this TransactionData data) => new()
  {
    Id = data.Id,
    CreatedAt = data.CreatedAt,
    Record = data.ToRecord(),
  };


  public static Transaction ToDomain(this TransactionData data) => new()
  {
    Principal = data.ToPrincipal(),
    Wallet = data.Wallet.ToPrincipal(),
  };


  // Domain -> Data
  public static TransactionData Update(this TransactionData data, TransactionRecord record)
  {
    data.Name = record.Name;
    data.Description = record.Description;

    data.Amount = record.Amount;
    data.TransactionType = (int)record.Type;

    data.To = record.To;
    data.From = record.From;
    return data;
  }
}
