namespace Domain.Withdrawal;

// Why a gateway refund could not be assigned to exactly one withdrawal, or
// why it was. Carried on every row of the report so the human reading the
// "unsure" list knows what to check rather than just that we gave up.
public enum RefundMatchVerdict
{
  // exactly one candidate withdrawal survived every filter
  Matched = 0,

  // zinc already has a fragment for this gateway refund id — nothing to do,
  // and reported so a re-run visibly accounts for every refund in the window
  AlreadyAttached = 1,

  // no zinc payment row carries this payment intent, so no withdrawal of ours
  // can own the refund (a refund against a booking payment, another product,
  // or an intent that predates zinc's payment records)
  NoPaymentIntent = 2,

  // the intent resolves to a wallet, but that wallet has no withdrawal the
  // refund could belong to
  NoCandidateWithdrawal = 3,

  // more than one candidate survived and the gateway data cannot separate
  // them — THE case the human asked to see listed
  Ambiguous = 4,
}

// One gateway refund, and what the reconciliation concluded about it.
public record RefundMatch
{
  public required GatewayRefund Refund { get; init; }

  public required RefundMatchVerdict Verdict { get; init; }

  // set only for Matched (and AlreadyAttached, where it names the withdrawal
  // the existing fragment already belongs to)
  public required Guid? WithdrawalId { get; init; }

  // the wallet the refund's payment intent resolved to, when it resolved
  public required string? UserId { get; init; }

  // Every withdrawal that survived the filters, best-scoring first. For
  // Ambiguous rows this IS the human's shortlist; for Matched it has exactly
  // one entry.
  public required IReadOnlyList<RefundCandidateScore> Candidates { get; init; }

  // one-line human-readable account of the verdict
  public required string Reason { get; init; }
}

// A candidate withdrawal and how well it fits the refund.
public record RefundCandidateScore
{
  public required Guid WithdrawalId { get; init; }

  public required decimal Amount { get; init; }

  public required WithdrawalMethod Method { get; init; }

  public required WithdrawStatus Status { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required DateTime? CompletedAt { get; init; }

  // |refund amount - the withdrawal's still-unevidenced amount|. Zero means
  // the refund exactly accounts for what the withdrawal has left to explain.
  public required decimal AmountGap { get; init; }

  // hours between the refund's creation and the withdrawal's reference time
  // (completed, else created); null when the gateway reported no created_at
  public required double? HoursApart { get; init; }
}

// The whole dry run: every refund in the window, bucketed.
public record RefundReconciliationReport
{
  public required DateTime FromUtc { get; init; }

  public required DateTime ToUtc { get; init; }

  // refunds the gateway reported in the window
  public required int Scanned { get; init; }

  public required IReadOnlyList<RefundMatch> Matched { get; init; }

  public required IReadOnlyList<RefundMatch> Ambiguous { get; init; }

  public required IReadOnlyList<RefundMatch> Unowned { get; init; }

  public required IReadOnlyList<RefundMatch> AlreadyAttached { get; init; }
}

// Pure matcher for historic gateway refunds -> owning withdrawals.
//
// The problem: WithdrawalMethod.CardRefund did not always exist. Refunds
// issued manually before it did left NO fragment behind, so the only record
// that a withdrawal was paid by card refund is the refund sitting at the
// gateway. Those withdrawals are stored as Method = PayNow.
//
// What the gateway gives us per refund is the payment intent, an amount, and a
// creation time. What it never gives us is the withdrawal id. So ownership is
// INFERRED, and the whole design point is that a weak inference is reported as
// unsure rather than written:
//
//  1. The intent resolves through zinc's payment rows to exactly one wallet.
//     A refund can then only belong to a withdrawal of that wallet — this is
//     the one hard, non-heuristic constraint, and it does the heavy lifting.
//  2. Within that wallet, only withdrawals that could have been paid out by a
//     refund survive: Completed (money left) and, so a half-finished manual
//     job is still visible, RequireManualIntervention.
//  3. A refund cannot predate the withdrawal that caused it, so candidates
//     created after the refund are dropped (with a small clock-skew grace).
//  4. What is left is scored on amount coverage first and time proximity
//     second. A single survivor whose amount fits within the tolerance is
//     Matched. Anything else is Ambiguous, with the shortlist attached.
//
// Deliberately NOT done: picking the closest candidate when several fit. A
// user with two PayNow withdrawals of the same amount in the same week is
// genuinely undecidable from gateway data, and quietly choosing one would
// write a false money trail into a tax export. That is strictly worse than a
// blank cell, which at least reads as "unknown".
public static class RefundReconciler
{
  // Airwallex keeps a refund queryable for at most 2 years since creation.
  // Same haircut as RefundArnBackfillRunner.RetentionWindow, and the same
  // reason: stay off a cliff edge we cannot observe.
  public static readonly TimeSpan RetentionWindow = RefundArnBackfillRunner.RetentionWindow;

  // How far a refund's amount may sit from a candidate's unexplained amount
  // and still count as covering it. A cent of rounding is plausible in
  // hand-entered historic refunds; a dollar is a different withdrawal.
  public const decimal AmountTolerance = 0.01m;

  // A refund settles after the withdrawal that caused it, never meaningfully
  // before. The grace absorbs clock skew and the manual case where an admin
  // issued the refund minutes before recording the withdrawal.
  public static readonly TimeSpan CreationGrace = TimeSpan.FromDays(1);

  // Statuses a historic card refund could have paid out. Pending/Rejected/
  // Cancel never moved money, so a refund cannot belong to them.
  private static readonly WithdrawStatus[] PayableStatuses =
  [
    WithdrawStatus.Completed,
    WithdrawStatus.RequireManualIntervention,
  ];

  public static RefundReconciliationReport Reconcile(
    DateTime fromUtc,
    DateTime toUtc,
    IReadOnlyList<GatewayRefund> refunds,
    IReadOnlyList<WithdrawalRefundFragment> existingFragments,
    IReadOnlyList<PaymentIntentOwner> intentOwners,
    IReadOnlyList<WithdrawalCandidate> candidates
  )
  {
    var attachedByRefundId = existingFragments
      .Where(f => f.AirwallexRefundId != null)
      .GroupBy(f => f.AirwallexRefundId!, StringComparer.Ordinal)
      .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    var ownerByIntent = intentOwners
      .GroupBy(o => o.PaymentIntentId, StringComparer.Ordinal)
      .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    var candidatesByWallet = candidates
      .GroupBy(c => c.WalletId)
      .ToDictionary(g => g.Key, g => (IReadOnlyList<WithdrawalCandidate>)g.ToList());

    // Fragments this run has already assigned, so two refunds in the same
    // window cannot both claim the same withdrawal's one unexplained amount.
    // Without this a wallet with two same-amount refunds would match both to
    // the same withdrawal and the apply phase would over-attach.
    var claimed = new Dictionary<Guid, decimal>();

    var results = new List<RefundMatch>();
    foreach (var refund in refunds.OrderBy(r => r.CreatedAt ?? DateTime.MaxValue))
      results.Add(MatchOne(refund, attachedByRefundId, ownerByIntent, candidatesByWallet, claimed));

    return new RefundReconciliationReport
    {
      FromUtc = fromUtc,
      ToUtc = toUtc,
      Scanned = refunds.Count,
      Matched = results.Where(r => r.Verdict == RefundMatchVerdict.Matched).ToList(),
      Ambiguous = results.Where(r => r.Verdict == RefundMatchVerdict.Ambiguous).ToList(),
      Unowned = results
        .Where(r =>
          r.Verdict
            is RefundMatchVerdict.NoPaymentIntent
              or RefundMatchVerdict.NoCandidateWithdrawal
        )
        .ToList(),
      AlreadyAttached = results
        .Where(r => r.Verdict == RefundMatchVerdict.AlreadyAttached)
        .ToList(),
    };
  }

  private static RefundMatch MatchOne(
    GatewayRefund refund,
    Dictionary<string, WithdrawalRefundFragment> attachedByRefundId,
    Dictionary<string, PaymentIntentOwner> ownerByIntent,
    Dictionary<Guid, IReadOnlyList<WithdrawalCandidate>> candidatesByWallet,
    Dictionary<Guid, decimal> claimed
  )
  {
    // 0. Idempotency, before anything else: zinc already has evidence for this
    // refund, so a second fragment would double-count it in the refundable
    // pool and duplicate the cell in the export.
    if (attachedByRefundId.TryGetValue(refund.Id, out var existing))
      return Unresolved(
        refund,
        RefundMatchVerdict.AlreadyAttached,
        $"already evidenced by fragment '{existing.Id}' on withdrawal '{existing.WithdrawalId}'",
        withdrawalId: existing.WithdrawalId
      );

    // 1. The hard constraint: the intent must resolve to a wallet we know.
    if (!ownerByIntent.TryGetValue(refund.PaymentIntentId, out var owner))
      return Unresolved(
        refund,
        RefundMatchVerdict.NoPaymentIntent,
        $"no zinc payment carries intent '{refund.PaymentIntentId}', so no withdrawal can own it"
      );

    var wallet = candidatesByWallet.GetValueOrDefault(owner.WalletId, []);

    // 2/3. Only withdrawals that moved money, and that the refund cannot
    // predate.
    var eligible = wallet
      .Where(c => PayableStatuses.Contains(c.Status))
      .Where(c => refund.CreatedAt is null || c.CreatedAt <= refund.CreatedAt.Value + CreationGrace)
      .ToList();

    if (eligible.Count == 0)
      return Unresolved(
        refund,
        RefundMatchVerdict.NoCandidateWithdrawal,
        $"user '{owner.UserId}' has no completed withdrawal this refund could belong to",
        userId: owner.UserId
      );

    // 4. Score what is left. Unexplained = the net payout minus evidence
    // already attached (by an earlier run, or by an earlier refund in THIS
    // run) — so a partially-reconciled withdrawal is scored on what it still
    // has to account for, not on its full amount.
    var scored = eligible
      .Select(c =>
      {
        var net = c.Amount - (c.Fee ?? 0m);
        var unexplained = net - c.AttachedRefundTotal - claimed.GetValueOrDefault(c.Id);
        var reference = c.CompletedAt ?? c.CreatedAt;
        return new RefundCandidateScore
        {
          WithdrawalId = c.Id,
          Amount = c.Amount,
          Method = c.Method,
          Status = c.Status,
          CreatedAt = c.CreatedAt,
          CompletedAt = c.CompletedAt,
          AmountGap = Math.Abs(unexplained - refund.Amount),
          HoursApart = refund.CreatedAt is null
            ? null
            : Math.Abs((refund.CreatedAt.Value - reference).TotalHours),
        };
      })
      .OrderBy(s => s.AmountGap)
      .ThenBy(s => s.HoursApart ?? double.MaxValue)
      .ToList();

    var fitting = scored.Where(s => s.AmountGap <= AmountTolerance).ToList();

    if (fitting.Count == 0)
      return new RefundMatch
      {
        Refund = refund,
        Verdict = RefundMatchVerdict.Ambiguous,
        WithdrawalId = null,
        UserId = owner.UserId,
        Candidates = scored,
        Reason =
          $"no withdrawal of user '{owner.UserId}' has SGD {refund.Amount:0.00} left to account "
          + $"for (closest is off by SGD {scored[0].AmountGap:0.00}) — a partial refund, a fee "
          + "difference, or evidence already recorded elsewhere",
      };

    if (fitting.Count > 1)
      return new RefundMatch
      {
        Refund = refund,
        Verdict = RefundMatchVerdict.Ambiguous,
        WithdrawalId = null,
        UserId = owner.UserId,
        Candidates = fitting,
        Reason =
          $"{fitting.Count} withdrawals of user '{owner.UserId}' each account for SGD "
          + $"{refund.Amount:0.00} exactly — the gateway data cannot separate them",
      };

    var winner = fitting[0];
    claimed[winner.WithdrawalId] =
      claimed.GetValueOrDefault(winner.WithdrawalId) + refund.Amount;

    return new RefundMatch
    {
      Refund = refund,
      Verdict = RefundMatchVerdict.Matched,
      WithdrawalId = winner.WithdrawalId,
      UserId = owner.UserId,
      Candidates = fitting,
      Reason =
        $"the only withdrawal of user '{owner.UserId}' with SGD {refund.Amount:0.00} left to "
        + $"account for"
        + (winner.HoursApart is null ? "" : $", {winner.HoursApart:0.0}h from the refund"),
    };
  }

  private static RefundMatch Unresolved(
    GatewayRefund refund,
    RefundMatchVerdict verdict,
    string reason,
    Guid? withdrawalId = null,
    string? userId = null
  ) =>
    new()
    {
      Refund = refund,
      Verdict = verdict,
      WithdrawalId = withdrawalId,
      UserId = userId,
      Candidates = [],
      Reason = reason,
    };
}
