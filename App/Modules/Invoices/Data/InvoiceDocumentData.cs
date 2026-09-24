using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Invoices.Data;

// A stored partner invoice. See Domain/Invoice/InvoiceDocument.cs for why
// both halves are frozen rather than recomputed.
//
// The two JSON columns are stored as text holding serialized JSON rather than
// as EF owned entities. That is a deliberate departure from the OwnsOne().ToJson()
// convention used by BookingData.PriceBreakdown and PaymentData.Statuses:
// those shapes are small, stable and queried; this one is a deep tree of ~20
// record types whose ONLY job is to reproduce a document byte-for-byte years
// from now. Mapping it as owned entities would couple the on-disk format to
// the C# record layout, so renaming a domain property would silently change
// how every historical invoice deserializes — the exact failure freezing
// exists to prevent. As opaque text, an old document keeps deserializing
// through its own explicit DTOs no matter how the domain is refactored.
public class InvoiceDocumentData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // first day of the invoiced month (SGT). Unique among Issued rows.
  public DateOnly PeriodMonth { get; set; }

  [MaxLength(16)]
  public string Seq { get; set; } = string.Empty;

  // InvoiceStatus: 0 Draft, 1 Issued, 2 Void
  public byte Status { get; set; }

  // InvoiceTicketBasis: 0 StatusToday, 1 Ledger, 2 TranscribedFromIssued
  public byte TicketBasis { get; set; }

  public int EngineVersion { get; set; }

  public string InputsJson { get; set; } = string.Empty;

  public string ComputedJson { get; set; } = string.Empty;

  // Denormalized out of ComputedJson so the month list does not have to
  // deserialize every document to show what it paid. Never read for the
  // invoice itself — ComputedJson stays the single source of truth.
  [Precision(16, 8)]
  public decimal NetProfit { get; set; }

  [Precision(16, 8)]
  public decimal PoolTotal { get; set; }

  public DateOnly IssueDate { get; set; }

  public DateOnly DueDate { get; set; }

  [MaxLength(128)]
  public string? CreatedBy { get; set; }

  public DateTime? IssuedAt { get; set; }

  [MaxLength(128)]
  public string? IssuedBy { get; set; }

  public DateTime? VoidedAt { get; set; }

  [MaxLength(128)]
  public string? VoidedBy { get; set; }

  [MaxLength(512)]
  public string? VoidReason { get; set; }
}
