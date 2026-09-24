namespace App.Modules.Invoices.API.V1;

// which SGT month to gather, as "MM-yyyy" (the wire date convention used by
// every other reporting endpoint is dd-MM-yyyy; a month has no day, so this
// is the month-precision form of it)
public record InvoiceInputQueryReq(string Month);

public record InvoiceInputTerminatedRes(int Count, decimal KeptRevenue);

public record InvoiceInputPriorityRes(int Paid, decimal Fee, int Free);

public record InvoiceInputRouteRes(
  string Key,
  int Direction,
  int Tickets,
  decimal Revenue,
  InvoiceInputTerminatedRes Terminated,
  InvoiceInputPriorityRes Priority
);

public record InvoiceInputWithdrawalsRes(int Count, decimal Total, decimal Income, int WithFee);

public record InvoiceInputFeesRes(decimal Gateway, decimal PaymentMethod);

// The month's KTMB card funding. Myr/Sgd are the two sums whose ratio is the
// FX rate the invoice converts fares at — a measured rate, not a quoted one.
//
// Both zero means no top-up posted in this month, which is a real answer for a
// month before the issuing sweep existed. It must be shown as "not available"
// rather than used as a rate.
public record InvoiceInputTopupsRes(decimal Myr, decimal Sgd);

public record InvoiceInputRowRes(
  string Month,
  decimal GrossDeposits,
  InvoiceInputFeesRes Fees,
  decimal RefundFeesExcluded,
  IEnumerable<InvoiceInputRouteRes> Routes,
  InvoiceInputWithdrawalsRes Withdrawals,
  InvoiceInputTopupsRes Topups
);

// ---- settings ----

// Queue a change to the agreed terms. EffectiveAt omitted = immediate.
public record SetInvoiceSettingsReq(
  decimal MarketingSharePct,
  decimal Infrastructure,
  decimal RecoveryPerBoost,
  decimal RecoveryPerTicket,
  DateTime? EffectiveAt
);

// Queue a change to one partner's terms, identified by Suffix. Setting
// Active = false retires the partner without deleting any history.
public record SetInvoicePartnerReq(
  string Suffix,
  string Name,
  string RoundingPreference,
  bool Active,
  int Position,
  DateTime? EffectiveAt
);

public record InvoiceSettingsChangeRes(
  Guid Id,
  decimal MarketingSharePct,
  decimal Infrastructure,
  decimal RecoveryPerBoost,
  decimal RecoveryPerTicket,
  DateTime EffectiveAt,
  DateTime CreatedAt
);

public record InvoicePartnerChangeRes(
  Guid Id,
  string Suffix,
  string Name,
  string RoundingPreference,
  bool Active,
  int Position,
  DateTime EffectiveAt,
  DateTime CreatedAt
);

public record InvoicePartnerRes(string Suffix, string Name, string RoundingPreference);

public record InvoiceTermsRes(
  decimal MarketingSharePct,
  decimal Infrastructure,
  decimal RecoveryPerBoost,
  decimal RecoveryPerTicket,
  IEnumerable<InvoicePartnerRes> Partners
);

// Current is null when the terms are not usable yet — either no settings row
// is effective, or no partner is. The UI must show "not configured" rather
// than treating a missing share as zero.
public record InvoiceSettingsRes(
  InvoiceTermsRes? Current,
  IEnumerable<InvoiceSettingsChangeRes> Upcoming,
  IEnumerable<InvoicePartnerChangeRes> UpcomingPartners
);

// ---- preview ----
//
// The whole month goes over the wire. The caller assembles it (from
// GET inputs, GET settings and its own judgement on the pieces zinc does not
// yet gather), and this computes it without persisting anything.
//
// Taking the input rather than a month string is deliberate: the operator
// reviewing a draft needs to change a figure and immediately see what it does
// to the payable. A month-string endpoint could only ever show one answer.

public record PreviewPeriodReq(string Label, string MonthName, string Seq);

public record PreviewTopupReq(string Date, decimal Rm, decimal Sgd);

public record PreviewFeesReq(decimal Gateway, decimal PaymentMethod);

public record PreviewTerminatedReq(int Count, decimal KeptRevenue, decimal HalfFareSgd);

public record PreviewRouteReq(
  string Key,
  string Label,
  string Short,
  int Tickets,
  decimal Revenue,
  decimal FareRm,
  PreviewTerminatedReq Terminated
);

public record PreviewWithdrawalsReq(int Count, decimal Total);

public record PreviewPartnerReq(string Suffix, string Name, string RoundingPreference);

public record PreviewPriorityRouteReq(int Paid, decimal Fee, int Free);

public record PreviewPriorityReq(
  Dictionary<string, PreviewPriorityRouteReq> PerRoute,
  decimal KeptOnCancelled,
  int KeptOnCancelledCount
);

public record PreviewCoverageReq(int WithBreakdown, int Total);

// Kind is "policy" (a surcharge, positive) or "discount" (negative).
public record PreviewPriceLineReq(string Kind, string Name, int Count, decimal Delta);

public record PreviewSurchargeReq(
  PreviewCoverageReq Coverage,
  Dictionary<string, PreviewPriceLineReq[]> PerRoute
);

public record PreviewWithdrawalFeeReq(decimal Income, int WithFee, int Count);

public record PreviewPromotionalReq(int Count, decimal Amount);

public record PreviewDuplicatesReq(int Count, decimal Refunded);

public record PreviewRecoveryReq(int FreeBoosts, int Tickets, decimal PerBoost, decimal PerTicket);

public record PreviewInvoiceReq(
  PreviewPeriodReq Period,
  string IssueDate,
  string DueDate,
  PreviewTopupReq[] Topups,
  string? TopupNote,
  PreviewFeesReq Fees,
  decimal GrossDeposits,
  decimal RefundFeesExcluded,
  PreviewRouteReq[] Routes,
  PreviewWithdrawalsReq Withdrawals,
  decimal Infrastructure,
  decimal MarketingSharePct,
  PreviewPartnerReq[] Partners,
  PreviewPriorityReq Priority,
  PreviewSurchargeReq Surcharge,
  PreviewWithdrawalFeeReq WithdrawalFee,
  PreviewPromotionalReq Promotional,
  decimal NetTransfers,
  PreviewDuplicatesReq Duplicates,
  PreviewRecoveryReq? PartnerRecovery,
  // Pin the blended fee % to what an already-issued invoice printed. Only for
  // reproducing a historical document; leave null for a new month.
  decimal? FeeRateOverride,
  // Likewise for a wasted-fee figure the formula cannot reproduce.
  decimal? WastedFeeOverride
);

// ---- computed ----

public record InvoiceFxRes(
  decimal TotalFundedRm,
  decimal TotalFundedSgd,
  // full precision — this is what is applied per route
  decimal FxRate,
  // the 5dp value the document shows
  decimal FxRatePrinted
);

public record InvoiceFeeRes(decimal TotalPaymentFees, decimal FeeRatePct);

public record InvoicePriorityRouteRes(
  int Paid,
  decimal Fee,
  int Free,
  decimal Gross,
  decimal FeeCost,
  decimal Net
);

public record InvoicePriceLineRes(string Kind, string Name, int Count, decimal Delta);

public record InvoiceComputedRouteRes(
  string Key,
  string Label,
  string Short,
  int Tickets,
  decimal Revenue,
  decimal FareRm,
  PreviewTerminatedReq Terminated,
  decimal TicketFaresRm,
  decimal TicketFaresSgd,
  decimal ProcessingFee,
  decimal DirectCost,
  decimal Contribution,
  decimal MarginPct,
  decimal TerminatedNet,
  InvoicePriorityRouteRes Priority,
  IEnumerable<InvoicePriceLineRes> PriceLines,
  decimal SurchargeGross,
  decimal DiscountGross
);

public record InvoiceTotalsRes(
  int Tickets,
  decimal Revenue,
  decimal PricePerTicket,
  decimal TicketFaresRm,
  decimal TicketFaresSgd,
  decimal ProcessingFee,
  decimal DirectCost,
  decimal Contribution,
  decimal TotalMarginPct
);

public record InvoiceAdjustmentsRes(
  decimal TerminatedNet,
  int TerminatedCount,
  // what the invoice uses
  decimal WastedFee,
  // what the formula gives — kept alongside so an override's variance stays
  // visible rather than absorbed
  decimal WastedFeeComputed,
  decimal Infrastructure
);

public record InvoiceAncillaryPriorityRes(
  decimal Gross,
  decimal FeeCost,
  decimal Net,
  int Paid,
  int Free,
  decimal Kept,
  int KeptCount
);

public record InvoiceAncillarySurchargeRes(
  decimal Gross,
  decimal Discounts,
  PreviewCoverageReq Coverage,
  // below 100 means these figures are UNDERSTATED — never extrapolate, show
  // the shortfall
  decimal CoveragePct
);

public record InvoiceAncillaryRes(
  InvoiceAncillaryPriorityRes Priority,
  // reported, never added — already inside Revenue
  InvoiceAncillarySurchargeRes Surcharge,
  PreviewWithdrawalFeeReq WithdrawalFee,
  decimal Promotional,
  decimal NetTransfers,
  PreviewDuplicatesReq Duplicates,
  decimal Total
);

public record InvoiceRecoveryRes(
  int FreeBoosts,
  int Tickets,
  decimal PerBoost,
  decimal PerTicket,
  decimal Boosts,
  decimal TicketsAmount,
  decimal Total,
  bool Applies
);

public record InvoiceShareRes(
  string Name,
  string Suffix,
  string RoundingPreference,
  decimal Pct,
  // the agreed profit share, unchanged by the recovery
  decimal Earned,
  // already collected outside the system — held, so not paid again
  decimal Advance,
  // what we actually transfer
  decimal Amount
);

public record InvoiceResultRes(
  decimal NetProfit,
  decimal NetMarginPct,
  decimal ShareBase,
  decimal MarketingSharePool,
  IEnumerable<InvoiceShareRes> Shares
);

public record InvoiceComputedRes(
  InvoiceFxRes Fx,
  InvoiceFeeRes Fee,
  IEnumerable<InvoiceComputedRouteRes> Routes,
  InvoiceTotalsRes Totals,
  InvoiceAdjustmentsRes Adjustments,
  InvoiceAncillaryRes Ancillary,
  InvoiceRecoveryRes Recovery,
  InvoiceResultRes Result
);

// ---- stored invoices ----
//
// Status and TicketBasis cross the wire as strings, matching how the enums in
// the preview payload are handled: an invoice's status is read by a human on
// a screen, and "issued" survives a renumbering of the enum where 1 does not.

// Save (or replace) the draft for a month. The inputs are the same payload
// the preview endpoint takes, so an operator can preview, adjust, preview
// again, and then save exactly what they were looking at.
public record SaveInvoiceDraftReq(
  // first day of the invoiced month, dd-MM-yyyy like every other date on the
  // wire in this codebase
  string PeriodMonth,
  string Seq,
  string TicketBasis,
  string IssueDate,
  string DueDate,
  PreviewInvoiceReq Inputs
);

// Withdraw an issued invoice. The reason is required: "there is a void
// invoice for August and nobody remembers why" is the failure this prevents.
public record VoidInvoiceReq(string Reason);

// Record a month that was invoiced before this system existed.
//
// June, July and August were produced by the invoices/ toolchain and sent as
// PDFs. Without this they stay outside the system and the database is a
// parallel record rather than the record — the first question anyone asks of
// a new invoice page is "what did we pay last month", and it has to be
// answerable there.
//
// ATTEST is what makes this safe. The caller does not supply the figures; it
// supplies the ones printed on the document it holds, and the server computes
// the rest from Inputs and refuses if the two disagree. So a transcription
// error becomes a 409 rather than a wrong number sitting in the system of
// record wearing the authority of a settled invoice.
public record TranscribeInvoiceReq(
  string PeriodMonth,
  string Seq,
  string IssueDate,
  string DueDate,
  // when the document actually went out, ISO-8601. Not "now": the row is a
  // record of something that already happened.
  DateTime IssuedAt,
  PreviewInvoiceReq Inputs,
  TranscribeAttestReq Attest
);

// The figures read off the paper document, as a check on the transcription.
//
// Deliberately only four. Every intermediate is downstream of these, so a
// transposed input that leaves all four intact is not one that changed what
// anybody was paid — and demanding forty numbers off a PDF would make the
// backfill so tedious it got done carelessly.
public record TranscribeAttestReq(
  int Tickets,
  decimal Revenue,
  decimal NetProfit,
  // partner suffix -> the amount that partner was actually transferred
  IDictionary<string, decimal> Amounts
);

// A month in the list. Deliberately without the frozen payload — the list
// page needs what exists and what it paid, nothing more.
public record InvoiceSummaryRes(
  Guid Id,
  string PeriodMonth,
  string Seq,
  string Status,
  string TicketBasis,
  int EngineVersion,
  string IssueDate,
  string DueDate,
  decimal NetProfit,
  decimal PoolTotal,
  DateTime CreatedAt,
  DateTime? IssuedAt
);

// One stored invoice, with both frozen halves.
//
// Computed is what this invoice PAID. For an issued invoice it is read
// straight out of storage and the calculator is never called — see
// Domain/Invoice/InvoiceDocument.cs.
public record InvoiceDocumentRes(
  Guid Id,
  string PeriodMonth,
  string Seq,
  string Status,
  string TicketBasis,
  int EngineVersion,
  string IssueDate,
  string DueDate,
  PreviewInvoiceReq Inputs,
  InvoiceComputedRes Computed,
  DateTime CreatedAt,
  string? CreatedBy,
  DateTime? IssuedAt,
  string? IssuedBy,
  DateTime? VoidedAt,
  string? VoidedBy,
  string? VoidReason
);

// What today's engine would compute over this invoice's frozen inputs, and
// where that differs from what it actually paid. A REPORT: nothing here is
// ever written back to the invoice.
public record InvoiceDriftRes(
  Guid Id,
  bool HasDrift,
  int FrozenEngineVersion,
  int CurrentEngineVersion,
  IEnumerable<InvoiceDriftFieldRes> Fields
);

public record InvoiceDriftFieldRes(string Path, decimal Frozen, decimal Current, decimal Delta);
