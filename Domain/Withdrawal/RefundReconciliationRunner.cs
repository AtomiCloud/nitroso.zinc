using CSharp_Result;
using Microsoft.Extensions.Logging;

namespace Domain.Withdrawal;

// What an apply pass wrote.
public record RefundReconciliationApplyReport
{
  public required DateTime FromUtc { get; init; }

  public required DateTime ToUtc { get; init; }

  // confidently-matched refunds the dry run offered
  public required int Eligible { get; init; }

  // fragments actually created
  public required int Attached { get; init; }

  // matched refunds skipped because evidence appeared since the dry run
  public required int AlreadyAttached { get; init; }

  public required IReadOnlyList<RefundAttachment> Attachments { get; init; }
}

// One fragment the apply pass created, for the audit trail.
public record RefundAttachment
{
  public required Guid FragmentId { get; init; }

  public required Guid WithdrawalId { get; init; }

  public required string AirwallexRefundId { get; init; }

  public required string PaymentIntentId { get; init; }

  public required decimal Amount { get; init; }

  public required string? AcquirerReferenceNumber { get; init; }
}

// Two-phase reconciliation of historic gateway refunds against withdrawals.
//
// An admin-triggered pair of calls rather than a BackgroundService, unlike
// RefundArnBackfillWorker: this is a ONE-OFF historical repair whose matching
// is inferential, so a human must read the ambiguous bucket before anything is
// written. A recurring worker would either need to auto-apply guesses (writing
// a false money trail into a tax export) or accumulate a report nobody reads.
// The ARN backfill can be a worker precisely because its input is unambiguous:
// it already knows the refund id and only fetches a missing field.
//
// Phase 1 Report is read-only and safe to repeat. Phase 2 Apply writes
// fragments for the confidently-matched set ONLY, re-deriving the match from
// fresh data so the caller cannot smuggle in a withdrawal id of their own.
public class RefundReconciliationRunner(
  IWithdrawalRefundRepository refundRepo,
  IRefundGateway gateway,
  ILogger<RefundReconciliationRunner> logger
)
{
  public async Task<Result<RefundReconciliationReport>> Report(
    DateTime fromUtc,
    DateTime toUtc,
    DateTime nowUtc
  )
  {
    var horizon = nowUtc - RefundReconciler.RetentionWindow;
    // The gateway answers nothing before this, so a wider request would report
    // a silent, permanent gap as "no refunds found".
    var from = fromUtc < horizon ? horizon : fromUtc;
    if (fromUtc < horizon)
      logger.LogWarning(
        "Refund reconciliation: requested window starts {Requested} but the gateway retains "
          + "refunds only from {Horizon}; refunds created before that are unrecoverable and are "
          + "NOT in this report",
        fromUtc,
        horizon
      );

    if (from >= toUtc)
      return Empty(from, toUtc);

    var refundsR = await gateway.ListRefunds(from, toUtc);
    if (refundsR.IsFailure())
      return refundsR.FailureOrDefault();
    var refunds = refundsR.SuccessOrDefault();

    logger.LogInformation(
      "Refund reconciliation: {Count} gateway refund(s) in [{From}, {To})",
      refunds.Count,
      from,
      toUtc
    );
    if (refunds.Count == 0)
      return Empty(from, toUtc);

    var existingR = await refundRepo.ListByAirwallexRefundIds(refunds.Select(r => r.Id));
    if (existingR.IsFailure())
      return existingR.FailureOrDefault();

    var ownersR = await refundRepo.ListPaymentIntentOwners(
      refunds.Select(r => r.PaymentIntentId).Distinct(StringComparer.Ordinal)
    );
    if (ownersR.IsFailure())
      return ownersR.FailureOrDefault();
    var owners = ownersR.SuccessOrDefault();

    var candidatesR = await refundRepo.ListCandidatesByWallets(
      owners.Select(o => o.WalletId).Distinct()
    );
    if (candidatesR.IsFailure())
      return candidatesR.FailureOrDefault();

    var report = RefundReconciler.Reconcile(
      from,
      toUtc,
      refunds,
      existingR.SuccessOrDefault(),
      owners,
      candidatesR.SuccessOrDefault()
    );
    logger.LogInformation(
      "Refund reconciliation: {Matched} matched, {Ambiguous} ambiguous, {Unowned} unowned, "
        + "{Attached} already attached",
      report.Matched.Count,
      report.Ambiguous.Count,
      report.Unowned.Count,
      report.AlreadyAttached.Count
    );
    return report;
  }

  // Attaches fragments for the confidently-matched bucket only. The match is
  // re-derived here from a fresh Report rather than taken from the caller, so
  // an apply can never write an assignment a dry run would not have offered —
  // and a refund that became ambiguous since the dry run is skipped instead of
  // acted on.
  public async Task<Result<RefundReconciliationApplyReport>> Apply(
    DateTime fromUtc,
    DateTime toUtc,
    DateTime nowUtc
  )
  {
    var reportR = await this.Report(fromUtc, toUtc, nowUtc);
    if (reportR.IsFailure())
      return reportR.FailureOrDefault();
    var report = reportR.SuccessOrDefault();

    var attachments = new List<RefundAttachment>();
    var skipped = 0;

    foreach (var match in report.Matched)
    {
      // Re-check under the write: Report proved no fragment held this refund id
      // when it read, but a concurrent settle webhook or a second apply could
      // have written one since.
      var freshR = await refundRepo.ListByAirwallexRefundIds([match.Refund.Id]);
      if (freshR.IsFailure())
        return freshR.FailureOrDefault();
      if (freshR.SuccessOrDefault().Count > 0)
      {
        skipped++;
        continue;
      }

      var ownersR = await refundRepo.ListPaymentIntentOwners([match.Refund.PaymentIntentId]);
      if (ownersR.IsFailure())
        return ownersR.FailureOrDefault();
      var owner = ownersR.SuccessOrDefault().FirstOrDefault();
      if (owner is null)
      {
        // Report only matches refunds whose intent resolved, so this is
        // unreachable barring a concurrent payment deletion.
        skipped++;
        continue;
      }

      var createdR = await refundRepo.CreateMany([ToFragment(match, owner, nowUtc)]);
      if (createdR.IsFailure())
        return createdR.FailureOrDefault();
      var created = createdR.SuccessOrDefault().Single();

      logger.LogInformation(
        "Refund reconciliation: attached gateway refund '{RefundId}' to withdrawal "
          + "'{WithdrawalId}' as fragment '{FragmentId}'",
        match.Refund.Id,
        match.WithdrawalId,
        created.Id
      );
      attachments.Add(
        new RefundAttachment
        {
          FragmentId = created.Id,
          WithdrawalId = created.WithdrawalId,
          AirwallexRefundId = match.Refund.Id,
          PaymentIntentId = created.PaymentIntentId,
          Amount = created.Amount,
          AcquirerReferenceNumber = created.AcquirerReferenceNumber,
        }
      );
    }

    return new RefundReconciliationApplyReport
    {
      FromUtc = report.FromUtc,
      ToUtc = report.ToUtc,
      Eligible = report.Matched.Count,
      Attached = attachments.Count,
      AlreadyAttached = skipped,
      Attachments = attachments,
    };
  }

  private static WithdrawalRefundFragment ToFragment(
    RefundMatch match,
    PaymentIntentOwner owner,
    DateTime nowUtc
  )
  {
    var refund = match.Refund;
    var status = refund.Outcome switch
    {
      PayoutOutcome.Settled => RefundFragmentStatus.Settled,
      PayoutOutcome.Failed => RefundFragmentStatus.Failed,
      _ => RefundFragmentStatus.Created,
    };
    return new WithdrawalRefundFragment
    {
      Id = Guid.NewGuid(),
      WithdrawalId = match.WithdrawalId!.Value,
      PaymentId = owner.PaymentId,
      PaymentIntentId = refund.PaymentIntentId,
      AirwallexRefundId = refund.Id,
      AcquirerReferenceNumber = refund.AcquirerReferenceNumber,
      // Deliberately NOT the "{withdrawalId}-{attempt}-{index}" shape of a
      // zinc-issued fragment: these refunds were created by hand at the
      // gateway under a request id we never chose, so borrowing the attempt
      // shape would make a re-drive of the card-refund approve path think it
      // owned them. Keyed on the gateway refund id, which is unique, so the
      // unique index on RequestId is itself a second idempotency guard.
      RequestId = $"{match.WithdrawalId!.Value}-recon-{refund.Id}",
      Amount = refund.Amount,
      Status = status,
      // the refund's own creation time, not now: this row is evidence of when
      // the money actually moved, and the ARN backfill's retention bound reads
      // CreatedAt to decide whether the gateway can still be asked about it
      CreatedAt = refund.CreatedAt ?? nowUtc,
      SettledAt = status == RefundFragmentStatus.Settled
        ? refund.UpdatedAt ?? refund.CreatedAt ?? nowUtc
        : null,
    };
  }

  private static RefundReconciliationReport Empty(DateTime fromUtc, DateTime toUtc) =>
    new()
    {
      FromUtc = fromUtc,
      ToUtc = toUtc,
      Scanned = 0,
      Matched = [],
      Ambiguous = [],
      Unowned = [],
      AlreadyAttached = [],
    };
}
