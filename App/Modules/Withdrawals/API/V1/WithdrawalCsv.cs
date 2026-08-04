using System.Globalization;
using System.Text;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.API.V1;

// One fully-rendered line of the tax-evidence export. Every member is already
// a display string: the flattening (null payout -> blank, fragment lists ->
// ';'-joined) happens once, in ToExportRow, so the writer only quotes.
public record WithdrawalExportRow
{
  public required string WithdrawalId { get; init; }
  public required string CreatedAt { get; init; }
  public required string CompletedAt { get; init; }
  public required string Status { get; init; }
  public required string Method { get; init; }
  public required string UserId { get; init; }
  public required string UserEmail { get; init; }
  public required string GrossAmount { get; init; }
  public required string Fee { get; init; }
  public required string NetAmount { get; init; }
  public required string PayNowNumber { get; init; }
  public required string AirwallexConfirmation { get; init; }
  public required string PayoutAttempt { get; init; }
  public required string ReconcileAttempts { get; init; }
  public required string CompleterId { get; init; }
  public required string CompletionNote { get; init; }
  public required string ReceiptUrl { get; init; }
  public required string RefundPaymentIntentIds { get; init; }
  public required string RefundAirwallexIds { get; init; }
  public required string RefundAmounts { get; init; }
  public required string RefundStatuses { get; init; }
  public required string RefundSettledAts { get; init; }

  // The presigned receipt_url expires; this permanent object key is the
  // durable reference an admin can re-resolve long after the export.
  public required string ReceiptKey { get; init; }

  // Acquirer Reference Numbers, index-aligned with the other refund_*
  // columns. Blank slots are expected: the card network issues an ARN only
  // after a refund settles, and refunds older than the gateway's retention
  // window can never be backfilled.
  public required string RefundArns { get; init; }

  // Delegates to the one ordered column table so a cell can never drift out
  // of alignment with its header.
  public string[] ToFields() => WithdrawalCsv.Fields(this);
}

// RFC 4180 rendering for the withdrawal tax export. The file is opened in
// Excel/Sheets by an accountant, so two things matter beyond plain quoting:
// a UTF-8 BOM (so non-ASCII names survive) and formula-injection defanging.
public static class WithdrawalCsv
{
  // The single source of truth for the export contract: each header paired
  // with the cell that fills it. Headers and data rows are both derived from
  // this table, so a column can only be added or removed on both sides at
  // once — misalignment is impossible by construction.
  //
  // Column order is part of the contract with the accountant's importer —
  // append only, never reorder.
  private static readonly (string Header, Func<WithdrawalExportRow, string> Get)[] Columns =
  [
    ("withdrawal_id", r => r.WithdrawalId),
    ("created_at", r => r.CreatedAt),
    ("completed_at", r => r.CompletedAt),
    ("status", r => r.Status),
    ("method", r => r.Method),
    ("user_id", r => r.UserId),
    ("user_email", r => r.UserEmail),
    ("gross_amount", r => r.GrossAmount),
    ("fee", r => r.Fee),
    ("net_amount", r => r.NetAmount),
    ("paynow_number", r => r.PayNowNumber),
    ("airwallex_confirmation", r => r.AirwallexConfirmation),
    ("payout_attempt", r => r.PayoutAttempt),
    ("reconcile_attempts", r => r.ReconcileAttempts),
    ("completer_id", r => r.CompleterId),
    ("completion_note", r => r.CompletionNote),
    ("receipt_url", r => r.ReceiptUrl),
    ("refund_payment_intent_ids", r => r.RefundPaymentIntentIds),
    ("refund_airwallex_ids", r => r.RefundAirwallexIds),
    ("refund_amounts", r => r.RefundAmounts),
    ("refund_statuses", r => r.RefundStatuses),
    ("refund_settled_ats", r => r.RefundSettledAts),
    ("receipt_key", r => r.ReceiptKey),
    ("refund_arns", r => r.RefundArns),
  ];

  public static readonly string[] Headers = Columns.Select(c => c.Header).ToArray();

  internal static string[] Fields(WithdrawalExportRow row) =>
    Columns.Select(c => c.Get(row)).ToArray();

  // RFC 4180 mandates CRLF between records
  public const string LineEnding = "\r\n";

  public const string Bom = "\uFEFF";

  // Separator inside the six index-aligned refund_* columns. Gateway ids,
  // ARNs, fixed-point amounts, status names and ISO timestamps never contain
  // it, so the lists stay losslessly splittable.
  public const char RefundSeparator = ';';

  private const string DecimalFormat = "0.00";

  // Withdrawals have existed only since 2024, long after Singapore settled on
  // UTC+08:00 without daylight saving. A fixed offset keeps export rendering
  // deterministic without making this type depend on host tzdata.
  private static readonly TimeSpan SingaporeOffset = TimeSpan.FromHours(8);

  // Excel/Sheets evaluate a cell as a formula when it opens with one of
  // these; a leading tab or CR also lets the payload sneak past naive
  // filters. Prefixing with an apostrophe forces the cell to text.
  private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

  // Encoded through the same line writer as the data rows. Every header is a
  // lowercase snake_case identifier, so Field is a no-op on them and the
  // output is byte-identical to a plain join — but there is now only one
  // place that decides how a line is assembled.
  public static string HeaderLine => Line(Headers);

  // Defang first, then quote: the apostrophe must live INSIDE the quotes so
  // the spreadsheet sees it as the cell's first character.
  public static string Field(string? raw)
  {
    var value = raw ?? "";
    if (value.Length > 0 && FormulaTriggers.Contains(value[0]))
      value = "'" + value;

    var needsQuoting =
      value.Contains(',')
      || value.Contains('"')
      || value.Contains('\r')
      || value.Contains('\n');

    if (!needsQuoting)
      return value;

    return "\"" + value.Replace("\"", "\"\"") + "\"";
  }

  private static string Line(IEnumerable<string> fields) => string.Join(",", fields.Select(Field));

  public static string Line(WithdrawalExportRow row) => Line(row.ToFields());

  // Fixed 2dp, invariant culture: no thousands separator, no currency symbol
  public static string Amount(decimal value) =>
    value.ToString(DecimalFormat, CultureInfo.InvariantCulture);

  // Export timestamps in the same business timezone that defines the
  // repository's Before/After calendar-day filters. Database values returned
  // as Unspecified are UTC in this stack, never server-local wall-clock time.
  public static string Timestamp(DateTime value)
  {
    var singapore = new DateTimeOffset(ToUtc(value)).ToOffset(SingaporeOffset);
    return singapore.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
  }

  public static string Timestamp(DateTime? value) => value is null ? "" : Timestamp(value.Value);

  private static DateTime ToUtc(DateTime value) =>
    value.Kind switch
    {
      DateTimeKind.Utc => value,
      DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
      _ => value.ToUniversalTime(),
    };

  private static string JoinFragments(
    IReadOnlyList<WithdrawalRefundFragment> fragments,
    Func<WithdrawalRefundFragment, string?> select
  ) => string.Join(RefundSeparator, fragments.Select(f => select(f) ?? ""));

  // Domain -> one export line.
  //
  // Fee/net stay BLANK for terminal no-payment statuses (Pending, Rejected,
  // Cancel) even when stale approval-time payout data remains: emitting gross
  // as net there would overstate the deduction, and a 0.00 would wrongly
  // imply money moved.
  //
  // They also stay blank for a null payout in Processing and
  // RequireManualIntervention. Those statuses were appended alongside the
  // payout feature and Approve always writes a payout before a row can reach
  // them, so a null payout there is impossible by construction — and even if
  // one appeared, that money is initiated, not settled, so blank is honest.
  public static WithdrawalExportRow ToExportRow(this Withdrawal w, string? receiptUrl)
  {
    var p = w.Principal;
    var payout = p.Payout;
    var fragments = w.Refunds;
    var isCardRefund = p.Record.Method == WithdrawalMethod.CardRefund;
    var hasPayment = payout is not null
      && p.Status.Status is not (WithdrawStatus.Pending or WithdrawStatus.Rejected or WithdrawStatus.Cancel);
    // Withdrawals completed before the payout columns existed (migration
    // 20260708060802, Jul 2026) have no payout row at all: the pre-fee
    // Complete transferred the full amount and charged nothing. Exporting
    // those as blank would empty both money columns for ~2.5 years of
    // history — exactly the rows a tax export exists to produce.
    var legacyCompleted = payout is null && p.Status.Status is WithdrawStatus.Completed;

    return new WithdrawalExportRow
    {
      WithdrawalId = p.Id.ToString(),
      CreatedAt = Timestamp(p.CreatedAt),
      CompletedAt = Timestamp(p.Complete?.CompletedAt),
      Status = p.Status.Status.ToRes(),
      Method = p.Record.Method.ToRes(),
      UserId = w.User.Id,
      UserEmail = w.User.Record.Email ?? "",
      GrossAmount = Amount(p.Record.Amount),
      Fee = hasPayment ? Amount(payout!.Fee)
        : legacyCompleted ? Amount(0m)
        : "",
      // the fee is retained by the business; the user receives gross - fee.
      // Snapshotted at approval, so it matches the transfer that happened —
      // never re-derived from the current fee configuration.
      NetAmount = hasPayment ? Amount(p.Record.Amount - payout!.Fee)
        : legacyCompleted ? Amount(p.Record.Amount)
        : "",
      PayNowNumber = isCardRefund ? "" : p.Record.PayNowNumber ?? "",
      AirwallexConfirmation = payout?.ConfirmationNumber ?? "",
      PayoutAttempt = payout is null ? "" : payout.Attempt.ToString(CultureInfo.InvariantCulture),
      ReconcileAttempts = payout is null
        ? ""
        : payout.ReconcileAttempts.ToString(CultureInfo.InvariantCulture),
      CompleterId = p.Complete?.CompleterId ?? "",
      CompletionNote = p.Complete?.Note ?? "",
      ReceiptUrl = receiptUrl ?? "",
      RefundPaymentIntentIds = isCardRefund
        ? JoinFragments(fragments, f => f.PaymentIntentId)
        : "",
      RefundAirwallexIds = isCardRefund
        ? JoinFragments(fragments, f => f.AirwallexRefundId)
        : "",
      RefundAmounts = isCardRefund ? JoinFragments(fragments, f => Amount(f.Amount)) : "",
      RefundStatuses = isCardRefund ? JoinFragments(fragments, f => f.Status.ToRes()) : "",
      RefundSettledAts = isCardRefund ? JoinFragments(fragments, f => Timestamp(f.SettledAt)) : "",
      // permanent storage key; receipt_url next to it is a presigned link
      // that expires, so this is what survives in an archived CSV
      ReceiptKey = p.Complete?.Receipt ?? "",
      RefundArns = isCardRefund
        ? JoinFragments(fragments, f => f.AcquirerReferenceNumber)
        : "",
    };
  }

  // filename hint: withdrawals-{after}_{before}.csv, with readable stand-ins
  // for an open-ended bound
  public static string FileName(string? after, string? before)
  {
    var from = string.IsNullOrWhiteSpace(after) ? "earliest" : after;
    var to = string.IsNullOrWhiteSpace(before) ? "latest" : before;
    return $"withdrawals-{Sanitize(from)}_{Sanitize(to)}.csv";
  }

  private static string Sanitize(string value)
  {
    var sb = new StringBuilder(value.Length);
    foreach (var c in value)
      sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
    return sb.ToString();
  }
}
