using App.Modules.Users.API.V1;
using App.Modules.Wallets.API.V1;
using App.Utility;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.API.V1;

public static class WithdrawalMapper
{
  // Domain -> RES
  public static string ToRes(this WithdrawStatus status) =>
    status switch
    {
      WithdrawStatus.Pending => "Pending",
      WithdrawStatus.Completed => "Completed",
      WithdrawStatus.Rejected => "Rejected",
      WithdrawStatus.Cancel => "Cancel",
      WithdrawStatus.Processing => "Processing",
      WithdrawStatus.RequireManualIntervention => "RequireManualIntervention",
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static WithdrawalStatusRes ToRes(this WithdrawalStatus status) =>
    new(status.Status.ToRes());

  public static WithdrawalCompleteRes ToRes(this WithdrawalComplete complete) =>
    new(complete.CompletedAt, complete.Note, complete.Receipt);

  public static string ToRes(this WithdrawalMethod method) =>
    method switch
    {
      WithdrawalMethod.PayNow => "PayNow",
      WithdrawalMethod.CardRefund => "CardRefund",
      _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };

  public static string ToRes(this RefundFragmentStatus status) =>
    status switch
    {
      RefundFragmentStatus.Created => "Created",
      RefundFragmentStatus.Settled => "Settled",
      RefundFragmentStatus.Failed => "Failed",
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static WithdrawalRecordRes ToRes(this WithdrawalRecord record) =>
    new(record.Amount, record.PayNowNumber, record.Method.ToRes());

  public static WithdrawalRefundRes ToRes(this WithdrawalRefundFragment fragment) =>
    new(
      fragment.PaymentIntentId,
      fragment.AirwallexRefundId,
      fragment.Amount,
      fragment.Status.ToRes(),
      fragment.CreatedAt,
      fragment.SettledAt,
      fragment.AcquirerReferenceNumber
    );

  public static WithdrawalPayoutRes ToRes(this WithdrawalPayout payout) =>
    new(payout.ConfirmationNumber, payout.Fee, payout.ReconcileAttempts);

  public static WithdrawalPrincipalRes ToRes(this WithdrawalPrincipal w) =>
    new(
      w.Id,
      w.CreatedAt,
      w.Status.ToRes(),
      w.Record.ToRes(),
      w.Complete?.ToRes(),
      w.Payout?.ToRes()
    );

  public static WithdrawalRes ToRes(this Withdrawal w) =>
    new(
      w.Principal.ToRes(),
      w.User.ToRes(),
      w.Completer?.ToRes(),
      w.Wallet.ToRes(),
      w.Refunds.Select(x => x.ToRes())
    );

  public static string ToRes(this RefundMatchVerdict verdict) =>
    verdict switch
    {
      RefundMatchVerdict.Matched => "Matched",
      RefundMatchVerdict.AlreadyAttached => "AlreadyAttached",
      RefundMatchVerdict.NoPaymentIntent => "NoPaymentIntent",
      RefundMatchVerdict.NoCandidateWithdrawal => "NoCandidateWithdrawal",
      RefundMatchVerdict.Ambiguous => "Ambiguous",
      _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, null),
    };

  public static string ToRes(this PayoutOutcome outcome) =>
    outcome switch
    {
      PayoutOutcome.Settled => "Settled",
      PayoutOutcome.Failed => "Failed",
      PayoutOutcome.InFlight => "InFlight",
      PayoutOutcome.NotFound => "NotFound",
      _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

  public static RefundCandidateRes ToRes(this RefundCandidateScore score) =>
    new(
      score.WithdrawalId,
      score.Amount,
      score.Method.ToRes(),
      score.Status.ToRes(),
      score.CreatedAt,
      score.CompletedAt,
      score.AmountGap,
      score.HoursApart
    );

  public static RefundMatchRes ToRes(this RefundMatch match) =>
    new(
      match.Refund.Id,
      match.Refund.PaymentIntentId,
      match.Refund.Amount,
      match.Refund.Outcome.ToRes(),
      match.Refund.CreatedAt,
      match.Refund.AcquirerReferenceNumber,
      match.Verdict.ToRes(),
      match.WithdrawalId,
      match.UserId,
      match.Reason,
      match.Candidates.Select(c => c.ToRes())
    );

  public static RefundReconciliationRes ToRes(this RefundReconciliationReport report) =>
    new(
      report.FromUtc,
      report.ToUtc,
      report.Scanned,
      report.Matched.Select(m => m.ToRes()),
      report.Ambiguous.Select(m => m.ToRes()),
      report.Unowned.Select(m => m.ToRes()),
      report.AlreadyAttached.Select(m => m.ToRes())
    );

  public static RefundAttachmentRes ToRes(this RefundAttachment attachment) =>
    new(
      attachment.FragmentId,
      attachment.WithdrawalId,
      attachment.AirwallexRefundId,
      attachment.PaymentIntentId,
      attachment.Amount,
      attachment.AcquirerReferenceNumber
    );

  public static RefundReconciliationApplyRes ToRes(
    this RefundReconciliationApplyReport report
  ) =>
    new(
      report.FromUtc,
      report.ToUtc,
      report.Eligible,
      report.Attached,
      report.AlreadyAttached,
      report.Attachments.Select(a => a.ToRes())
    );

  public static string ToRes(this PayNowMode mode) =>
    mode switch
    {
      PayNowMode.Enabled => "Enabled",
      PayNowMode.Disabled => "Disabled",
      PayNowMode.FallbackOnly => "FallbackOnly",
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

  public static WithdrawalSettingsRes ToRes(this WithdrawalSettingsRecord record) =>
    new(record.CardRefundEnabled, record.PayNowMode.ToRes(), record.SweepEnabled);

  // REQ -> Domain
  public static PayNowMode ToPayNowMode(this string mode) =>
    mode switch
    {
      "Enabled" => PayNowMode.Enabled,
      "Disabled" => PayNowMode.Disabled,
      "FallbackOnly" => PayNowMode.FallbackOnly,
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

  public static WithdrawalSettingsRecord ToDomain(this SetWithdrawalSettingsReq req) =>
    new()
    {
      CardRefundEnabled = req.CardRefundEnabled,
      PayNowMode = req.PayNowMode.ToPayNowMode(),
      SweepEnabled = req.SweepEnabled,
    };

  public static WithdrawalMethod ToWithdrawalMethod(this string? method) =>
    method switch
    {
      // absent = PayNow: already-deployed frontends predate the method field
      null or "" or "PayNow" => WithdrawalMethod.PayNow,
      "CardRefund" => WithdrawalMethod.CardRefund,
      _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };
  public static WithdrawStatus ToWithdrawStatus(this string status) =>
    status switch
    {
      "Pending" => WithdrawStatus.Pending,
      "Completed" => WithdrawStatus.Completed,
      "Rejected" => WithdrawStatus.Rejected,
      "Cancel" => WithdrawStatus.Cancel,
      "Processing" => WithdrawStatus.Processing,
      "RequireManualIntervention" => WithdrawStatus.RequireManualIntervention,
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static WithdrawalRecord ToDomain(this CreateWithdrawalReq record)
  {
    var method = record.Method.ToWithdrawalMethod();
    return new WithdrawalRecord
    {
      Amount = record.Amount,
      Method = method,
      // card refunds carry no PayNow id even if one slipped into the request
      PayNowNumber = method == WithdrawalMethod.CardRefund ? null : record.PayNowNumber,
    };
  }

  public static WithdrawalSearch ToDomain(this SearchWithdrawalQuery query) =>
    new()
    {
      Id = query.Id,
      UserId = query.UserId,
      CompleterId = query.CompleterId,
      Min = query.Min,
      Max = query.Max,
      Status = query.Status?.ToWithdrawStatus(),
      Before = query.Before?.ToDate(),
      After = query.After?.ToDate(),
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };

  public static WithdrawalSearch ToDomain(this ExportWithdrawalQuery query) =>
    new()
    {
      Id = query.Id,
      UserId = query.UserId,
      CompleterId = query.CompleterId,
      Min = query.Min,
      Max = query.Max,
      Status = query.Status?.ToWithdrawStatus(),
      Before = query.Before?.ToDate(),
      After = query.After?.ToDate(),
      // Export paging uses a separate explicit limit and cursor, so the
      // normal-search pagination defaults are ignored.
    };
}
