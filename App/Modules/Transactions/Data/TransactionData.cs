using System.ComponentModel.DataAnnotations;
using App.Modules.Payments.Data;
using App.Modules.Wallets.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Transactions.Data;

public class TransactionData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // Record
  [MaxLength(256)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(8192)]
  public string Description { get; set; } = string.Empty;

  public short TransactionType { get; set; }

  [Precision(16, 8)]
  public decimal Amount { get; set; }

  [MaxLength(128)]
  public string From { get; set; } = string.Empty;

  [MaxLength(128)]
  public string To { get; set; } = string.Empty;

  // FK
  public Guid WalletId { get; set; }
  public WalletData Wallet { get; set; } = null!;

  public Guid? PaymentId { get; set; }
  public PaymentData? Payment { get; set; }
}
