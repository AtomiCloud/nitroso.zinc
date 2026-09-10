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
