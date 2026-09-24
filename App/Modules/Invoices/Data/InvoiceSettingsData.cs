using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Invoices.Data;

// Insert-only, effective-dated queue of the invoice's agreed terms (same
// convention as KtmbCostData): the newest row with EffectiveAt <= the
// instant asked about is live. Rows are never updated or deleted, so an
// invoice issued under old terms stays explicable.
public class InvoiceSettingsData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // the instant these terms start applying; a future date queues them,
  // CreatedAt (the default) makes them immediate
  public DateTime EffectiveAt { get; set; }

  [Precision(9, 4)]
  public decimal MarketingSharePct { get; set; }

  [Precision(16, 8)]
  public decimal Infrastructure { get; set; }

  [Precision(16, 8)]
  public decimal RecoveryPerBoost { get; set; }

  [Precision(16, 8)]
  public decimal RecoveryPerTicket { get; set; }
}

// Insert-only, effective-dated per-partner terms. Suffix is the partner's
// stable identity across rows (NOT unique — one partner has many rows over
// time); the effective set is resolved per suffix.
public class InvoicePartnerData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime EffectiveAt { get; set; }

  [MaxLength(8)]
  public string Suffix { get; set; } = string.Empty;

  [MaxLength(128)]
  public string Name { get; set; } = string.Empty;

  // InvoiceRoundingPreference: 0 down, 1 up
  public byte RoundingPreference { get; set; }

  // false removes the partner from the effective set without deleting history
  public bool Active { get; set; }

  public int Position { get; set; }
}
