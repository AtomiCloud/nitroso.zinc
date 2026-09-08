using App.Modules.Users.API.V1;
using App.Modules.Wallets.API.V1;

namespace App.Modules.Withdrawals.API.V1;

public record SearchWithdrawalQuery(
  Guid? Id,
  string? UserId,
  string? CompleterId,
  decimal? Min,
  decimal? Max,
  string? Status,
  string? Before,
  string? After,
  int? Limit,
  int? Skip
);

// An export covers every matching withdrawal, so pagination is deliberately
// absent from the public contract. The exporter supplies its own page size.
public record ExportWithdrawalQuery(
  Guid? Id,
  string? UserId,
  string? CompleterId,
  decimal? Min,
  decimal? Max,
  string? Status,
  string? Before,
  string? After
);

// Method: "PayNow" (default when omitted, rollout compat) or "CardRefund".
// PayNowNumber is required for PayNow and must be null/absent for CardRefund.
public record CreateWithdrawalReq(decimal Amount, string? PayNowNumber, string? Method);

public record CancelWithdrawalReq(string Note);

public record RejectWithdrawalReq(string Note);

// RESP
public record WithdrawalStatusRes(string Status);

public record WithdrawalCompleteRes(DateTime CompletedAt, string Note, string? Receipt);

public record WithdrawalRecordRes(decimal Amount, string? PayNowNumber, string Method);

public record WithdrawalPayoutRes(string? ConfirmationNumber, decimal Fee, int ReconcileAttempts);

// Card-refund evidence: one row per refund created against a funding payment
// intent, so the user can see exactly where each slice of the money went
public record WithdrawalRefundRes(
  string PaymentIntentId,
  string? AirwallexRefundId,
  decimal Amount,
  string Status,
  DateTime CreatedAt,
  DateTime? SettledAt,
  // card-network reference, null until the refund settles and the capture or
  // backfill sweep has seen it
  string? AcquirerReferenceNumber
);

public record WithdrawalPrincipalRes(
  Guid Id,
  DateTime CreateAt,
  WithdrawalStatusRes Status,
  WithdrawalRecordRes Record,
  WithdrawalCompleteRes? Complete,
  WithdrawalPayoutRes? Payout
);

// DEPRECATED: rollout-compat shape for GET Withdrawal/fee — use the Fee
// controller's FeeSpecRes instead, which also carries the flat component
public record FeeRes(decimal WithdrawFeeRate);

public record WithdrawalRes(
  WithdrawalPrincipalRes Principal,
  UserPrincipalRes User,
  UserPrincipalRes? Completer,
  WalletPrincipalRes Wallet,
  IEnumerable<WithdrawalRefundRes> Refunds
);

// GET Withdrawal/refundable/{userId}: how much the user could withdraw via
// card refunds right now, and the window that pool was computed over
public record RefundablePoolRes(decimal Pool, int WindowDays);

// Historic-refund reconciliation window (owner-only). Dates are inclusive
// calendar days in the export's business timezone, like the export query.
// Bounded by the gateway's 2-year refund retention whatever is asked for.
public record ReconcileRefundsQuery(string? After, string? Before);

// One candidate withdrawal a historic refund might belong to, with the two
// signals that ranked it. AmountGap 0 means the withdrawal has exactly this
// refund's amount left to account for.
public record RefundCandidateRes(
  Guid WithdrawalId,
  decimal Amount,
  string Method,
  string Status,
  DateTime CreatedAt,
  DateTime? CompletedAt,
  decimal AmountGap,
  double? HoursApart
);

// One gateway refund and what the reconciliation concluded. Candidates is the
// shortlist to check by hand when Verdict is "Ambiguous".
public record RefundMatchRes(
  string AirwallexRefundId,
  string PaymentIntentId,
  decimal Amount,
  string RefundStatus,
  DateTime? RefundCreatedAt,
  string? AcquirerReferenceNumber,
  string Verdict,
  Guid? WithdrawalId,
  string? UserId,
  string Reason,
  IEnumerable<RefundCandidateRes> Candidates
);

// GET Withdrawal/reconcile-refunds: the read-only comb. Ambiguous is the
// "unsure" list a human must resolve by hand; nothing is ever written for it.
public record RefundReconciliationRes(
  DateTime FromUtc,
  DateTime ToUtc,
  int Scanned,
  IEnumerable<RefundMatchRes> Matched,
  IEnumerable<RefundMatchRes> Ambiguous,
  IEnumerable<RefundMatchRes> Unowned,
  IEnumerable<RefundMatchRes> AlreadyAttached
);

// One fragment an apply pass created.
public record RefundAttachmentRes(
  Guid FragmentId,
  Guid WithdrawalId,
  string AirwallexRefundId,
  string PaymentIntentId,
  decimal Amount,
  string? AcquirerReferenceNumber
);

// POST Withdrawal/reconcile-refunds: what the apply pass wrote. Only the
// confidently-matched bucket is ever attached.
public record RefundReconciliationApplyRes(
  DateTime FromUtc,
  DateTime ToUtc,
  int Eligible,
  int Attached,
  int AlreadyAttached,
  IEnumerable<RefundAttachmentRes> Attachments
);

// POST Withdrawal/settings: replace the withdrawal method policy and the tin
// sweep switch. PayNowMode: "Enabled" | "Disabled" | "FallbackOnly" (PayNow
// accepted only when the refundable pool cannot cover the amount).
public record SetWithdrawalSettingsReq(
  bool CardRefundEnabled,
  string PayNowMode,
  bool SweepEnabled
);

// GET Withdrawal/settings/current: the settings in effect right now
// (defaults when never configured). Any logged-in caller may read it — the
// create dialog needs the method availability, and tin polls SweepEnabled.
public record WithdrawalSettingsRes(
  bool CardRefundEnabled,
  string PayNowMode,
  bool SweepEnabled
);
