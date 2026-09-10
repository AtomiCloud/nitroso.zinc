using CSharp_Result;

namespace Domain.Invoice;

// A partner invoice as a stored document.
//
// The point of this type is FREEZING. An invoice that has been issued and
// paid must show the same figures forever, and it cannot do that by being
// recomputed on demand, for two independent reasons:
//
//   1. The inputs move. Booking status is mutated in place, so re-gathering
//      June today reports 2,672 completed tickets where the issued invoice
//      says 2,713 — the difference is bookings refunded after issue. Nothing
//      is wrong with either number; they answer different questions.
//   2. The engine moves. This project has already corrected the recovery
//      treatment and waived a free-boost deduction mid-flight. A future fix
//      is a fix for future months, not a silent restatement of a settled one.
//
// So an Issued invoice stores BOTH halves — the inputs it was computed from
// and the outputs it printed — and rendering reads the stored outputs. The
// calculator is never called for an issued document.
//
// Storing inputs as well is what makes drift detectable: re-running today's
// engine over the frozen inputs and diffing against the frozen outputs
// isolates engine change from input change. That is a REPORT, never an
// auto-correction.
public enum InvoiceStatus : byte
{
  // freely recomputed, freely edited, not a document yet
  Draft = 0,

  // frozen; the figures below are what was sent to the partners
  Issued = 1,

  // withdrawn after issue. Kept, never deleted: an invoice that was sent and
  // then retracted is part of the record, and the reason is the record.
  Void = 2,
}

// Where the ticket counts on this invoice came from. Recorded per invoice
// because it is not knowable from the figures themselves, and June's answer
// differs from every month after it.
public enum InvoiceTicketBasis : byte
{
  // booking status as it read at the moment of issue. This is how July and
  // August were produced, and it is the default for new months.
  StatusToday = 0,

  // the append-only booking ledger, which counts every booking that ever
  // completed regardless of what happened afterwards. Reads 2,881 for June.
  Ledger = 1,

  // transcribed from a document issued before this system existed. The
  // figures are the issued PDF's, not this engine's, and may not reproduce.
  TranscribedFromIssued = 2,
}

// Bumped whenever InvoiceCalculator's arithmetic changes. Stored on every
// invoice so a drift report can say WHICH engine produced the frozen numbers
// rather than just that they differ.
//
// Version 1 is the engine as ported from invoices/calc.ts, pinned to the
// issued June, July and August documents.
public static class InvoiceEngine
{
  public const int Version = 1;
}

public record InvoiceDocumentRecord
{
  // first day of the invoiced month, SGT. The unique key for issued
  // invoices — one issued invoice per month, enforced in the database.
  public required DateOnly PeriodMonth { get; init; }

  // the human reference on the document, e.g. "0901". Not unique: each
  // partner's copy appends their own suffix.
  public required string Seq { get; init; }

  public required InvoiceStatus Status { get; init; }

  public required InvoiceTicketBasis TicketBasis { get; init; }

  public required int EngineVersion { get; init; }

  public required DateOnly IssueDate { get; init; }

  public required DateOnly DueDate { get; init; }
}

public record InvoiceDocument
{
  public required Guid Id { get; init; }

  public required InvoiceDocumentRecord Record { get; init; }

  // The frozen halves, as the JSON actually stored. They are carried as text
  // rather than as parsed records because the renderer and the API both want
  // to pass the document straight through, and because an invoice frozen
  // under an older shape must survive being read back even if the current
  // records no longer match it. Parsing is the caller's decision, and only
  // the drift check needs it.
  public required string InputsJson { get; init; }

  public required string ComputedJson { get; init; }

  // The money, pulled out of ComputedJson at write time. This is what the
  // drift check compares and what the list page shows.
  public required InvoiceFigures Figures { get; init; }

  public required DateTime CreatedAt { get; init; }

  public string? CreatedBy { get; init; }

  public DateTime? IssuedAt { get; init; }

  public string? IssuedBy { get; init; }

  public DateTime? VoidedAt { get; init; }

  public string? VoidedBy { get; init; }

  // why it was withdrawn. Required to void, because "there is a void invoice
  // for August and nobody remembers why" is the failure this prevents.
  public string? VoidReason { get; init; }
}

// A summary row for the month list, without the two JSON documents. The
// frozen payload of a single month is large and the list page needs none of
// it — only enough to show what exists and what it paid.
public record InvoiceDocumentSummary
{
  public required Guid Id { get; init; }

  public required InvoiceDocumentRecord Record { get; init; }

  public required decimal NetProfit { get; init; }

  public required decimal PoolTotal { get; init; }

  public required DateTime CreatedAt { get; init; }

  public DateTime? IssuedAt { get; init; }
}

// The result of re-running the current engine over an invoice's frozen
// inputs. Reported, never applied.
public record InvoiceDrift
{
  public required Guid Id { get; init; }

  // the engine that produced the frozen figures, vs the one running now
  public required int FrozenEngineVersion { get; init; }

  public required int CurrentEngineVersion { get; init; }

  public required IEnumerable<InvoiceDriftField> Fields { get; init; }

  public bool HasDrift => this.Fields.Any();
}

public record InvoiceDriftField
{
  // dotted path into the computed document, e.g. "result.netProfit" or
  // "shares.C.amount"
  public required string Path { get; init; }

  public required decimal Frozen { get; init; }

  public required decimal Current { get; init; }

  public decimal Delta => this.Current - this.Frozen;
}

// What a caller asks to be saved. Id, timestamps and the frozen JSON are the
// repository's to produce — the caller supplies the month and the terms.
public record InvoiceDocumentDraft
{
  public required DateOnly PeriodMonth { get; init; }

  public required string Seq { get; init; }

  public required InvoiceTicketBasis TicketBasis { get; init; }

  public required DateOnly IssueDate { get; init; }

  public required DateOnly DueDate { get; init; }

  public required InvoiceMonthInput Inputs { get; init; }
}

public interface IInvoiceDocumentRepository
{
  // newest month first; no JSON payloads
  Task<Result<IEnumerable<InvoiceDocumentSummary>>> List();

  Task<Result<InvoiceDocument?>> Get(Guid id);

  // the issued invoice for a month, if there is one
  Task<Result<InvoiceDocument?>> GetIssued(DateOnly periodMonth);

  // Computes and saves as a Draft. Overwrites the existing draft for the
  // month if there is one — a draft is scratch space, not a document.
  Task<Result<InvoiceDocument>> SaveDraft(InvoiceDocumentDraft draft, string? by);

  // Freezes a draft. Fails if the month already has an issued invoice: the
  // database enforces that, and this is where the collision becomes a
  // conflict rather than an exception.
  Task<Result<InvoiceDocument>> Issue(Guid id, string? by);

  // Withdraws an issued invoice. The row stays; only Status and the void
  // fields change, because a sent-then-retracted invoice is part of the
  // record.
  Task<Result<InvoiceDocument>> Void(Guid id, string reason, string? by);

  // Writes a document whose figures come from elsewhere — the backfill of
  // June, July and August from the invoices/ toolchain. Separate from
  // SaveDraft/Issue on purpose: this is the ONE path that stores figures the
  // current engine did not produce, so it is the one path an auditor has to
  // look at to ask "which of these numbers did this system actually compute?"
  Task<Result<InvoiceDocument>> Transcribe(
    InvoiceDocumentDraft draft,
    InvoiceComputed computed,
    DateTime issuedAt,
    string? by
  );
}
