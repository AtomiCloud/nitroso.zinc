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

public record InvoiceInputRowRes(
  string Month,
  decimal GrossDeposits,
  InvoiceInputFeesRes Fees,
  decimal RefundFeesExcluded,
  IEnumerable<InvoiceInputRouteRes> Routes,
  InvoiceInputWithdrawalsRes Withdrawals
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
