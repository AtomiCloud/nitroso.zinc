using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using App.Modules.Transactions.Data;
using App.Modules.Wallets.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Payments.Data;

public class PaymentStatusEntryData
{
  public string Status { get; set; } = string.Empty;
  public DateTime Updated { get; set; }
}

public class PaymentStatusData
{
  public List<PaymentStatusEntryData> Statuses { get; set; } = null!;
}

public class PaymentData
{
  public Guid Id { get; set; }

  // reference
  [MaxLength(256)] public string ExternalReference { get; set; } = null!;

  [MaxLength(32)] public string Gateway { get; set; } = null!;

  // immutable
  public DateTime CreatedAt { get; set; }


  // record
  [Precision(16, 8)] public decimal Amount { get; set; }

  [Precision(16, 8)] public decimal CapturedAmount { get; set; }

  [MaxLength(16)] public string Currency { get; set; } = null!;

  public DateTime LastUpdated { get; set; }

  [MaxLength(32)] public string Status { get; set; } = null!;

  public PaymentStatusData Statuses { get; set; } = null!;

  public JsonDocument AdditionalData { get; set; } = null!;

  // FKs
  public Guid WalletId { get; set; }
  public WalletData Wallet { get; set; } = null!;

  public TransactionData? Transaction { get; set; }
}
