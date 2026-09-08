using Domain.Withdrawal;
using FluentAssertions;

namespace UnitTest.Withdrawals;

// The matcher decides whether a historic gateway refund can be attributed to
// exactly one withdrawal. Its job is as much to REFUSE as to match: a wrong
// attribution writes a false money trail into a tax export, which is strictly
// worse than a blank cell that reads as "unknown".
public class RefundReconciliationTests
{
  private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime To = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

  private static readonly Guid Wallet = Guid.NewGuid();
  private const string User = "user-1";
  private static readonly Guid PaymentId = Guid.NewGuid();
  private const string Intent = "int_funding";

  [Fact]
  public void A_lone_completed_withdrawal_of_the_matching_amount_is_matched()
  {
    var withdrawal = Candidate(amount: 100m, completedAt: From.AddDays(10));

    var report = Reconcile([Refund("rfd_1", 100m, From.AddDays(10))], candidates: [withdrawal]);

    report.Matched.Should().HaveCount(1);
    report.Ambiguous.Should().BeEmpty();
    var match = report.Matched[0];
    match.WithdrawalId.Should().Be(withdrawal.Id);
    match.UserId.Should().Be(User);
    match.Candidates.Should().HaveCount(1);
    match.Candidates[0].AmountGap.Should().Be(0m);
  }

  // THE case the human asked to have listed. Two withdrawals of the same amount
  // around the same date are genuinely undecidable from gateway data: the
  // refund names an intent and an amount, and both fit equally.
  [Fact]
  public void Two_equally_fitting_withdrawals_are_ambiguous_and_both_are_listed()
  {
    var first = Candidate(amount: 100m, completedAt: From.AddDays(10));
    var second = Candidate(amount: 100m, completedAt: From.AddDays(12));

    var report = Reconcile(
      [Refund("rfd_1", 100m, From.AddDays(11))],
      candidates: [first, second]
    );

    report.Matched.Should().BeEmpty("guessing between them would fabricate evidence");
    report.Ambiguous.Should().HaveCount(1);
    var match = report.Ambiguous[0];
    match.WithdrawalId.Should().BeNull();
    match.Candidates.Select(c => c.WithdrawalId)
      .Should()
      .BeEquivalentTo([first.Id, second.Id], "the human needs the whole shortlist");
    match.Reason.Should().Contain("cannot separate them");
  }

  // Time proximity ranks the shortlist but must NEVER break a tie on its own:
  // an admin issuing a refund a day later than another is not evidence.
  [Fact]
  public void Time_proximity_alone_does_not_resolve_an_amount_tie()
  {
    var near = Candidate(amount: 100m, completedAt: From.AddDays(10));
    var far = Candidate(amount: 100m, completedAt: From.AddDays(60));

    var report = Reconcile([Refund("rfd_1", 100m, From.AddDays(10))], candidates: [near, far]);

    report.Ambiguous.Should().HaveCount(1);
    // ranked, so the human reads the likeliest first...
    report.Ambiguous[0].Candidates[0].WithdrawalId.Should().Be(near.Id);
    // ...but not decided
    report.Matched.Should().BeEmpty();
  }

  [Fact]
  public void A_refund_whose_intent_zinc_does_not_know_is_unowned()
  {
    var report = Reconcile(
      [Refund("rfd_1", 100m, From.AddDays(10), intent: "int_unknown")],
      candidates: [Candidate(amount: 100m, completedAt: From.AddDays(10))]
    );

    report.Unowned.Should().HaveCount(1);
    report.Unowned[0].Verdict.Should().Be(RefundMatchVerdict.NoPaymentIntent);
    report.Matched.Should().BeEmpty();
  }

  [Fact]
  public void A_wallet_with_no_completed_withdrawal_is_unowned()
  {
    var pending = Candidate(amount: 100m, completedAt: null, status: WithdrawStatus.Pending);

    var report = Reconcile([Refund("rfd_1", 100m, From.AddDays(10))], candidates: [pending]);

    report.Unowned.Should().HaveCount(1);
    report.Unowned[0].Verdict.Should().Be(RefundMatchVerdict.NoCandidateWithdrawal);
  }

  // A refund cannot belong to a withdrawal that did not exist yet.
  [Fact]
  public void A_withdrawal_created_after_the_refund_is_not_a_candidate()
  {
    var later = Candidate(
      amount: 100m,
      completedAt: From.AddDays(90),
      createdAt: From.AddDays(89)
    );

    var report = Reconcile([Refund("rfd_1", 100m, From.AddDays(10))], candidates: [later]);

    report.Unowned.Should().HaveCount(1);
    report.Unowned[0].Verdict.Should().Be(RefundMatchVerdict.NoCandidateWithdrawal);
  }

  // Idempotency: the run must be safe to repeat. A refund zinc already has
  // evidence for is reported, never attached twice — a second fragment would
  // double-count in the refundable pool and duplicate the export cell.
  [Fact]
  public void A_refund_zinc_already_has_a_fragment_for_is_never_matched_again()
  {
    var withdrawal = Candidate(amount: 100m, completedAt: From.AddDays(10));
    var existing = Fragment(withdrawal.Id, "rfd_1", 100m);

    var report = Reconcile(
      [Refund("rfd_1", 100m, From.AddDays(10))],
      candidates: [withdrawal],
      existing: [existing]
    );

    report.Matched.Should().BeEmpty();
    report.AlreadyAttached.Should().HaveCount(1);
    report.AlreadyAttached[0].WithdrawalId.Should().Be(withdrawal.Id);
  }

  // The fee snapshot matters: a card refund returns the NET, so the amount to
  // match against is gross - fee, not gross.
  [Fact]
  public void The_refunded_amount_is_matched_against_the_net_not_the_gross()
  {
    var withdrawal = Candidate(amount: 100m, completedAt: From.AddDays(10), fee: 4m);

    var report = Reconcile([Refund("rfd_1", 96m, From.AddDays(10))], candidates: [withdrawal]);

    report.Matched.Should().HaveCount(1);
    report.Matched[0].WithdrawalId.Should().Be(withdrawal.Id);
  }

  // Fragments already attached reduce what a withdrawal still has to explain,
  // so a partially-reconciled withdrawal is scored on the remainder. Without
  // this a second run would see the full gross unexplained and mismatch.
  [Fact]
  public void Evidence_already_attached_is_subtracted_before_scoring()
  {
    var withdrawal = Candidate(
      amount: 100m,
      completedAt: From.AddDays(10),
      attachedRefundTotal: 60m
    );

    var report = Reconcile(
      [Refund("rfd_new", 40m, From.AddDays(10))],
      candidates: [withdrawal],
      // the 60m already attached sits under a different refund id
      existing: [Fragment(withdrawal.Id, "rfd_old", 60m)]
    );

    report.Matched.Should().HaveCount(1, "only the unexplained 40 remains");
    report.Matched[0].WithdrawalId.Should().Be(withdrawal.Id);
  }

  // Two refunds in ONE window must not both claim the same withdrawal's single
  // unexplained amount — otherwise the apply phase over-attaches and the
  // withdrawal ends up with more refund evidence than money that moved.
  [Fact]
  public void Two_refunds_cannot_both_claim_the_same_unexplained_amount()
  {
    var withdrawal = Candidate(amount: 100m, completedAt: From.AddDays(10));

    var report = Reconcile(
      [Refund("rfd_1", 100m, From.AddDays(10)), Refund("rfd_2", 100m, From.AddDays(11))],
      candidates: [withdrawal]
    );

    report.Matched.Should().HaveCount(1, "the withdrawal only has 100 to account for");
    report.Matched[0].Refund.Id.Should().Be("rfd_1", "the earlier refund claims it");
    report.Ambiguous.Should().HaveCount(1);
    report.Ambiguous[0].Refund.Id.Should().Be("rfd_2");
  }

  // A partial refund does not account for a whole withdrawal, so it must not be
  // silently attached as though it did.
  [Fact]
  public void A_refund_that_covers_only_part_of_a_withdrawal_is_ambiguous()
  {
    var withdrawal = Candidate(amount: 100m, completedAt: From.AddDays(10));

    var report = Reconcile([Refund("rfd_1", 30m, From.AddDays(10))], candidates: [withdrawal]);

    report.Matched.Should().BeEmpty();
    report.Ambiguous.Should().HaveCount(1);
    report.Ambiguous[0].Reason.Should().Contain("left to account");
    // the near-miss is still shown, with its gap, so the human can judge
    report.Ambiguous[0].Candidates[0].AmountGap.Should().Be(70m);
  }

  // Manual historic refunds sit on PayNow withdrawals precisely because
  // CardRefund did not exist yet — the method must not disqualify a candidate.
  [Fact]
  public void A_paynow_withdrawal_is_a_valid_owner_of_a_historic_card_refund()
  {
    var paynow = Candidate(
      amount: 100m,
      completedAt: From.AddDays(10),
      method: WithdrawalMethod.PayNow
    );

    var report = Reconcile([Refund("rfd_1", 100m, From.AddDays(10))], candidates: [paynow]);

    report.Matched.Should().HaveCount(1);
    report.Matched[0].Candidates[0].Method.Should().Be(WithdrawalMethod.PayNow);
  }

  // A refund can only belong to a withdrawal of the wallet its intent resolves
  // to. This is the one hard, non-heuristic constraint in the matcher.
  [Fact]
  public void A_withdrawal_of_another_wallet_is_never_a_candidate()
  {
    var otherWallet = Candidate(
      amount: 100m,
      completedAt: From.AddDays(10),
      walletId: Guid.NewGuid()
    );

    var report = Reconcile([Refund("rfd_1", 100m, From.AddDays(10))], candidates: [otherWallet]);

    report.Unowned.Should().HaveCount(1);
    report.Unowned[0].Verdict.Should().Be(RefundMatchVerdict.NoCandidateWithdrawal);
  }

  // Every refund the gateway reported must appear in exactly one bucket, so a
  // human can account for the whole window rather than wonder what was dropped.
  [Fact]
  public void Every_scanned_refund_lands_in_exactly_one_bucket()
  {
    var withdrawal = Candidate(amount: 100m, completedAt: From.AddDays(10));

    var report = Reconcile(
      [
        Refund("rfd_matched", 100m, From.AddDays(10)),
        Refund("rfd_unowned", 50m, From.AddDays(10), intent: "int_unknown"),
        Refund("rfd_attached", 25m, From.AddDays(10)),
      ],
      candidates: [withdrawal],
      existing: [Fragment(withdrawal.Id, "rfd_attached", 25m)]
    );

    report.Scanned.Should().Be(3);
    var bucketed =
      report.Matched.Count
      + report.Ambiguous.Count
      + report.Unowned.Count
      + report.AlreadyAttached.Count;
    bucketed.Should().Be(3);
  }

  private static RefundReconciliationReport Reconcile(
    IReadOnlyList<GatewayRefund> refunds,
    IReadOnlyList<WithdrawalCandidate> candidates,
    IReadOnlyList<WithdrawalRefundFragment>? existing = null
  ) =>
    RefundReconciler.Reconcile(
      From,
      To,
      refunds,
      existing ?? [],
      [
        new PaymentIntentOwner
        {
          PaymentId = PaymentId,
          PaymentIntentId = Intent,
          WalletId = Wallet,
          UserId = User,
        },
      ],
      candidates
    );

  private static GatewayRefund Refund(
    string id,
    decimal amount,
    DateTime createdAt,
    string intent = Intent,
    PayoutOutcome outcome = PayoutOutcome.Settled
  ) =>
    new()
    {
      Id = id,
      PaymentIntentId = intent,
      Amount = amount,
      Outcome = outcome,
      AcquirerReferenceNumber = "12345678901234567890123",
      CreatedAt = createdAt,
      UpdatedAt = createdAt,
      RequestId = null,
    };

  private static WithdrawalCandidate Candidate(
    decimal amount,
    DateTime? completedAt,
    WithdrawStatus status = WithdrawStatus.Completed,
    WithdrawalMethod method = WithdrawalMethod.PayNow,
    decimal? fee = null,
    DateTime? createdAt = null,
    decimal attachedRefundTotal = 0m,
    Guid? walletId = null
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      WalletId = walletId ?? Wallet,
      UserId = User,
      Method = method,
      Status = status,
      Amount = amount,
      Fee = fee,
      CreatedAt = createdAt ?? From.AddDays(1),
      CompletedAt = completedAt,
      AttachedRefundTotal = attachedRefundTotal,
    };

  private static WithdrawalRefundFragment Fragment(
    Guid withdrawalId,
    string refundId,
    decimal amount
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      WithdrawalId = withdrawalId,
      PaymentId = PaymentId,
      PaymentIntentId = Intent,
      AirwallexRefundId = refundId,
      RequestId = $"{withdrawalId}-recon-{refundId}",
      Amount = amount,
      Status = RefundFragmentStatus.Settled,
      CreatedAt = From.AddDays(10),
      SettledAt = From.AddDays(10),
    };
}
