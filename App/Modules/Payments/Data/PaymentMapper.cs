using App.Modules.Transactions.Data;
using App.Modules.Wallets.Data;
using Domain.Payment;

namespace App.Modules.Payments.Data;

public static class PaymentMapper
{
  // to Domain
  public static PaymentRecord ToRecord(this PaymentData data) =>
    new()
    {
      Amount = data.Amount,
      CapturedAmount = data.CapturedAmount,
      Currency = data.Currency,
      LastUpdated = data.LastUpdated,
      Status = data.Status,
      AdditionalData = data.AdditionalData,
    };

  public static PaymentReference ToReference(this PaymentData data) =>
    new()
    {
      Id = data.Id,
      ExternalReference = data.ExternalReference,
      Gateway = data.Gateway,
    };

  public static PaymentPrincipal ToPrincipal(this PaymentData data) =>
    new()
    {
      Reference = data.ToReference(),
      Record = data.ToRecord(),
      CreatedAt = data.CreatedAt,
      Statuses = data
        .Statuses.Statuses.Select(x => new KeyValuePair<string, DateTime>(x.Status, x.Updated))
        .ToDictionary(),
    };

  public static Payment ToDomain(this PaymentData data) =>
    new()
    {
      Principal = data.ToPrincipal(),
      Wallet = data.Wallet.ToPrincipal(),
      Transaction = data.Transaction?.ToPrincipal(),
    };

  // To Data
  public static PaymentData ToData(this PaymentReference reference)
  {
    return new PaymentData
    {
      Id = reference.Id,
      ExternalReference = reference.ExternalReference,
      Gateway = reference.Gateway,
    };
  }

  public static PaymentData UpdateData(this PaymentData data, PaymentRecord record)
  {
    data.Amount = record.Amount;
    data.CapturedAmount = record.CapturedAmount;
    data.Currency = record.Currency;
    data.LastUpdated = record.LastUpdated;
    data.Status = record.Status;
    data.AdditionalData = record.AdditionalData;

    return data;
  }
}
