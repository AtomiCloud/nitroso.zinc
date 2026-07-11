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
  [Precision(16, 8)]
  public decimal Amount { get; set; }

  // 0 = PayNow (transfer to the user's mobile), 1 = CardRefund (refunds
  // against the card payments that funded the wallet)
  public byte Method { get; set; }

  // empty for CardRefund withdrawals — no PayNow id is involved (kept
  // non-null to match the pre-method column shape)
  [MaxLength(64)]
  public string PayNowNumber { get; set; } = string.Empty;

  // Complete
  public DateTime? CompletedAt { get; set; }

  [MaxLength(4096)]
  public string? Note { get; set; }

  [MaxLength(64)]
  public string? Receipt { get; set; }

  // Payout (automated withdrawal bookkeeping; null Fee = never approved)
  [MaxLength(64)]
  public string? ConfirmationNumber { get; set; }

  [Precision(16, 8)]
  public decimal? Fee { get; set; }

  public int PayoutAttempt { get; set; }

  public int ReconcileAttempts { get; set; }

  // References
  public string? CompleterId { get; set; }
  public UserData? Completer { get; set; }

  public Guid WalletId { get; set; }
  public WalletData Wallet { get; set; } = null!;

  // card-refund evidence rows; empty for PayNow withdrawals
  public List<WithdrawalRefundData> Refunds { get; set; } = [];
}
