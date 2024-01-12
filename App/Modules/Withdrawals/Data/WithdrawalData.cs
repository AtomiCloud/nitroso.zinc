using System.ComponentModel.DataAnnotations;
using App.Modules.Users.Data;
using App.Modules.Wallets.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Withdrawals.Data;

public class WithdrawalData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // Status
  public byte Status { get; set; }

  // Record
  [Precision(16, 8)] public decimal Amount { get; set; }

  [MaxLength(64)] public string PayNowNumber { get; set; } = string.Empty;

  // Complete
  public DateTime? CompletedAt { get; set; }

  [MaxLength(4096)] public string? Note { get; set; }

  [MaxLength(64)] public string? Receipt { get; set; }

  // References
  public string? CompleterId { get; set; }
  public UserData? Completer { get; set; }

  public Guid WalletId { get; set; }
  public WalletData Wallet { get; set; } = null!;
}
