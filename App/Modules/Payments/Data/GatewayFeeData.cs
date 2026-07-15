using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Payments.Data;

// One Airwallex "financial transaction" (the gateway's own fee ledger),
// captured after the fact by the gateway-fee sync. FinancialTransactionId is
// the gateway's globally unique id and the idempotent upsert key; SourceId
// ties the row back to the object that moved the money (a payment intent, a
// payout transfer or a card refund — see SourceType) or, for account-level
// fees, the gateway's own billing source.
public class GatewayFeeData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // the intent/transfer/refund id this fee was reported against
  [MaxLength(256)]
  public string SourceId { get; set; } = string.Empty;

  // Domain.Payment.GatewayFeeSourceType: 0 Payment, 1 Transfer, 2 Refund,
  // 3 AccountFee
  public byte SourceType { get; set; }

  // Airwallex financial transaction id — unique, the upsert key
  [MaxLength(256)]
  public string FinancialTransactionId { get; set; } = string.Empty;

  [Precision(16, 8)]
  public decimal Amount { get; set; }

  [Precision(16, 8)]
  public decimal Fee { get; set; }

  [Precision(16, 8)]
  public decimal Net { get; set; }

  [MaxLength(16)]
  public string Currency { get; set; } = string.Empty;

  // the gateway's created_at for the financial transaction — the instant the
  // analysis page buckets fees on (SGT calendar convention downstream)
  public DateTime TransactedAt { get; set; }
}
