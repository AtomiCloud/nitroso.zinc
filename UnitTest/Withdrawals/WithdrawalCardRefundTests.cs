using CSharp_Result;
using Domain;
using Domain.Exceptions;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;
using Domain.Withdrawal;
using FluentAssertions;

namespace UnitTest.Withdrawals;

// The card-refund withdrawal rail: refundable-pool computation, oldest-first
// fragment planning, approve routing per method, refund webhook transitions
// and their money-safety invariants. The PayNow rail's regression coverage
// lives in WithdrawalServiceGuardTests (unchanged).
public class WithdrawalCardRefundTests
{
  private const decimal Amount = 100m;
  private const decimal Fee = 4m;
  private const decimal Net = Amount - Fee; // 96

  private static readonly Guid WalletId = Guid.NewGuid();

  // ---- harness ----

  private sealed class Harness
  {
    public required WithdrawalService Service { get; init; }
    public required FakeWithdrawalRepository Repo { get; init; }
    public required FakeWalletRepository Wallet { get; init; }
    public required FakeTransactionRepository Txn { get; init; }
    public required FakeRefundRepository Refunds { get; init; }
    public required FakeRefundGateway Gateway { get; init; }
    public required FakePayoutGateway PayNowGateway { get; init; }
  }

  private static Harness Make(Withdrawal? withdrawal)
  {
    var repo = new FakeWithdrawalRepository(withdrawal);
    var wallet = new FakeWalletRepository(withdrawal);
    var txn = new FakeTransactionRepository();
    var refunds = new FakeRefundRepository();
    var gateway = new FakeRefundGateway();
    var payNowGateway = new FakePayoutGateway();
    var service = new WithdrawalService(
      repo,
      wallet,
      txn,
      new TransactionGenerator(),
      new FakeWithdrawalStorage(),
      new PassThroughTransactionManager(),
      new FourPercentFeeCalculator(),
      payNowGateway,
      refunds,
      gateway,
      // both rails wide open: this suite exercises the card rail's money
      // mechanics, not the method policy (WithdrawalSettingsPolicyTests)
      new FakeWithdrawalSettingsRepository(
        new WithdrawalSettingsRecord
        {
          CardRefundEnabled = true,
          PayNowMode = PayNowMode.Enabled,
          SweepEnabled = false,
        }
      )
    );
    return new Harness
    {
      Service = service,
      Repo = repo,
      Wallet = wallet,
      Txn = txn,
      Refunds = refunds,
      Gateway = gateway,
      PayNowGateway = payNowGateway,
    };
  }

  private static Withdrawal WithdrawalWith(
    WithdrawStatus status,
    WithdrawalMethod method = WithdrawalMethod.CardRefund,
    WithdrawalPayout? payout = null
  )
  {
    var id = Guid.NewGuid();
    return new Withdrawal
    {
      Principal = new WithdrawalPrincipal
      {
        Id = id,
        CreatedAt = DateTime.UtcNow,
        Status = new WithdrawalStatus { Status = status },
        Record = new WithdrawalRecord
        {
          Amount = Amount,
          Method = method,
          PayNowNumber = method == WithdrawalMethod.PayNow ? "91234567" : null,
        },
        Complete = null,
        Payout = payout,
      },
      Wallet = new WalletPrincipal
      {
        Id = WalletId,
        UserId = "user-1",
        Record = new WalletRecord
        {
          Usable = 0m,
          WithdrawReserve = Amount,
          BookingReserve = 0m,
        },
      },
      User = new UserPrincipal
      {
        Id = "user-1",
        Record = new UserRecord { Username = "tester" },
      },
      Completer = null,
    };
  }

  private static FundingPayment Payment(decimal captured, int ageDays, string? intent = null) =>
    new()
    {
      PaymentId = Guid.NewGuid(),
      PaymentIntentId = intent ?? $"int_{Guid.NewGuid():N}",
      CreatedAt = DateTime.UtcNow.AddDays(-ageDays),
      CapturedAmount = captured,
    };

  // ---- RefundPlanner (pure fragment planning) ----

  [Fact]
  public void Plan_single_payment_exact_cover()
  {
    var p = Refundable(Payment(Net, 10));
    var plan = RefundPlanner.Plan(Net, [p]);

    plan.IsSuccess().Should().BeTrue();
    plan.SuccessOrDefault().Should().ContainSingle();
    plan.SuccessOrDefault()[0].Amount.Should().Be(Net);
  }

  [Fact]
  public void Plan_splits_across_payments_oldest_first()
  {
    var oldest = Refundable(Payment(50m, 90));
    var middle = Refundable(Payment(30m, 40));
    var newest = Refundable(Payment(100m, 5));
    // deliberately passed out of order — the planner must sort by CreatedAt
    var plan = RefundPlanner.Plan(Net, [newest, middle, oldest]);

    plan.IsSuccess().Should().BeTrue();
    var fragments = plan.SuccessOrDefault();
    fragments.Should().HaveCount(3);
    fragments[0].Payment.PaymentId.Should().Be(oldest.PaymentId, "oldest payment drains first");
    fragments[0].Amount.Should().Be(50m);
    fragments[1].Payment.PaymentId.Should().Be(middle.PaymentId);
    fragments[1].Amount.Should().Be(30m);
    fragments[2].Payment.PaymentId.Should().Be(newest.PaymentId);
    fragments[2].Amount.Should().Be(16m, "only the remainder is taken from the newest");
  }

  [Fact]
  public void Plan_short_pool_is_a_distinguishable_error()
  {
    var plan = RefundPlanner.Plan(Net, [Refundable(Payment(40m, 10))]);

    plan.IsSuccess().Should().BeFalse();
    var e = plan.FailureOrDefault().Should().BeOfType<InsufficientRefundablePoolException>().Which;
    e.Required.Should().Be(Net);
    e.Available.Should().Be(40m);
  }

  [Fact]
  public void Plan_skips_fully_refunded_payments()
  {
    var drained = Refundable(Payment(50m, 90)) with { Refundable = 0m };
    var live = Refundable(Payment(Net, 10));
    var plan = RefundPlanner.Plan(Net, [drained, live]);

    plan.IsSuccess().Should().BeTrue();
    plan.SuccessOrDefault().Should().ContainSingle();
    plan.SuccessOrDefault()[0].Payment.PaymentId.Should().Be(live.PaymentId);
  }

  private static RefundablePayment Refundable(FundingPayment p) =>
    new()
    {
      PaymentId = p.PaymentId,
      PaymentIntentId = p.PaymentIntentId,
      CreatedAt = p.CreatedAt,
      Refundable = p.CapturedAmount,
    };

  // ---- Refundable pool ----

  [Fact]
  public async Task Pool_subtracts_non_failed_fragments_and_respects_the_window()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    var inWindow = Payment(100m, 30);
    var alsoIn = Payment(50m, 179);
    h.Refunds.FundingPayments = [inWindow, alsoIn];
    // window filtering happens in the repository (SQL); the fake honors it
    h.Refunds.OutOfWindowPayments = [Payment(500m, 181)];
    // 60 already refunded against the first intent (Created counts, Failed
    // does not — the repository only sums non-Failed rows)
    h.Refunds.RefundedByPayment[inWindow.PaymentId] = 60m;

    var pool = await h.Service.RefundablePool("user-1");

    pool.IsSuccess().Should().BeTrue();
    pool.SuccessOrDefault().Should().Be(40m + 50m);
  }

  // ---- Create (pool pre-check) ----

  [Fact]
  public async Task Create_card_refund_with_covering_pool_reserves_normally()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(200m, 10)];

    var result = await h.Service.Create(
      "user-1",
      new WithdrawalRecord
      {
        Amount = Amount,
        Method = WithdrawalMethod.CardRefund,
        PayNowNumber = null,
      }
    );

    result.IsSuccess().Should().BeTrue();
    h.Wallet.PrepareWithdrawCalls.Should().Be(1);
    h.Txn.Records.Should().ContainSingle(r => r.Type == TransactionType.WithdrawRequest);
  }

  [Fact]
  public async Task Create_card_refund_with_short_pool_is_rejected_before_reserving()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(10m, 10)];

    var result = await h.Service.Create(
      "user-1",
      new WithdrawalRecord
      {
        Amount = Amount,
        Method = WithdrawalMethod.CardRefund,
        PayNowNumber = null,
      }
    );

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InsufficientRefundablePoolException>();
    h.Wallet.PrepareWithdrawCalls.Should().Be(0, "a hopeless request must not lock funds");
    h.Txn.Records.Should().BeEmpty();
  }

  [Fact]
  public async Task Create_paynow_never_touches_the_pool()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending, WithdrawalMethod.PayNow);
    var h = Make(w);
    // no funding payments at all — a PayNow create must not care

    var result = await h.Service.Create(
      "user-1",
      new WithdrawalRecord
      {
        Amount = Amount,
        Method = WithdrawalMethod.PayNow,
        PayNowNumber = "91234567",
      }
    );

    result.IsSuccess().Should().BeTrue();
    h.Refunds.PoolQueries.Should().Be(0);
  }

  // ---- Approve routing ----

  [Fact]
  public async Task Approve_card_refund_plans_fragments_and_creates_refunds_oldest_first()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    var older = Payment(50m, 90, "int_A");
    var newer = Payment(100m, 5, "int_B");
    h.Refunds.FundingPayments = [newer, older];

    var result = await h.Service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    h.Repo.StatusWrites.Should().Contain(s => s.Status == WithdrawStatus.Processing);
    h.Gateway.Requests.Should().HaveCount(2);
    h.Gateway.Requests[0].PaymentIntentId.Should().Be("int_A", "oldest payment drains first");
    h.Gateway.Requests[0].Amount.Should().Be(50m);
    h.Gateway.Requests[0].RequestId.Should().Be($"{w.Principal.Id}-1-0");
    h.Gateway.Requests[1].PaymentIntentId.Should().Be("int_B");
    h.Gateway.Requests[1].Amount.Should().Be(Net - 50m);
    h.Gateway.Requests[1].RequestId.Should().Be($"{w.Principal.Id}-1-1");
    // evidence rows persisted with the gateway refund ids
    h.Refunds.Fragments.Should().HaveCount(2);
    h.Refunds.Fragments.Should().OnlyContain(f => f.AirwallexRefundId != null);
    // confirmation number = FIRST fragment's refund id
    h.Repo.LastPayoutWritten!.ConfirmationNumber
      .Should()
      .Be(h.Refunds.Fragments[0].AirwallexRefundId);
    h.Wallet.WithdrawCalls.Should().Be(0, "no money is collected until settlement");
    h.PayNowGateway.Requests.Should().BeEmpty("the card rail must never create a transfer");
  }

  [Fact]
  public async Task Approve_card_refund_with_shrunken_pool_reverts_to_pending()
  {
    // the pool covered the amount at creation but shrank before approval
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(10m, 10)];

    var result = await h.Service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<InsufficientRefundablePoolException>();
    h.Repo.StatusWrites.Select(s => s.Status)
      .Should()
      .ContainInOrder(WithdrawStatus.Processing, WithdrawStatus.Pending);
    h.Gateway.Requests.Should().BeEmpty("no refund may exist when the plan fails");
    h.Refunds.Fragments.Should().BeEmpty();
    h.Wallet.WithdrawCalls.Should().Be(0);
  }

  [Fact]
  public async Task Approve_paynow_still_routes_to_the_transfer_rail()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending, WithdrawalMethod.PayNow);
    var h = Make(w);

    var result = await h.Service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    h.PayNowGateway.Requests.Should().ContainSingle();
    h.PayNowGateway.Requests[0].Amount.Should().Be(Net);
    h.Gateway.Requests.Should().BeEmpty("PayNow must never create card refunds");
    h.Refunds.Fragments.Should().BeEmpty();
  }

  [Fact]
  public async Task Approve_card_refund_partial_gateway_failure_stays_processing()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(50m, 90, "int_A"), Payment(100m, 5, "int_B")];
    h.Gateway.FailFromRequest = 1; // first refund lands, second dies

    var result = await h.Service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    h.Repo.StatusWrites.Select(s => s.Status)
      .Should()
      .Equal([WithdrawStatus.Processing], "ambiguous mid-fragment failure keeps the claim");
    h.Refunds.Fragments.Should().HaveCount(2, "the evidence rows precede the gateway calls");
    h.Refunds.Fragments[0].AirwallexRefundId.Should().NotBeNull();
    h.Refunds.Fragments[1].AirwallexRefundId.Should().BeNull();
  }

  [Fact]
  public async Task Approve_redrive_reuses_existing_fragments_and_request_ids()
  {
    // first attempt: fragment 0 created at the gateway, fragment 1 failed to
    // send; the re-drive must re-send ONLY the unconfirmed fragment with the
    // SAME request id, never re-plan
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(50m, 90, "int_A"), Payment(100m, 5, "int_B")];
    h.Gateway.FailFromRequest = 1;
    (await h.Service.Approve(w.Principal.Id)).IsSuccess().Should().BeFalse();

    h.Gateway.FailFromRequest = null;
    var redrive = await h.Service.Approve(w.Principal.Id);

    redrive.IsSuccess().Should().BeTrue();
    h.Gateway.Requests.Should().HaveCount(3, "2 initial sends + 1 re-send of the failed one");
    h.Gateway.Requests[2].RequestId
      .Should()
      .Be($"{w.Principal.Id}-1-1", "the re-drive reuses attempt 1's deterministic id");
    h.Refunds.Fragments.Should().HaveCount(2, "no re-planning on re-drive");
    h.Refunds.Fragments.Should().OnlyContain(f => f.AirwallexRefundId != null);
    h.Repo.LastPayoutWritten!.ConfirmationNumber
      .Should()
      .Be(h.Refunds.Fragments[0].AirwallexRefundId);
  }

  // ---- SettleRefundFragment (webhook success) ----

  private static WithdrawalPayout Claimed(int attempt = 1) =>
    new()
    {
      ConfirmationNumber = null,
      Fee = Fee,
      Attempt = attempt,
    };

  private async Task<(Harness H, Withdrawal W)> ApprovedCardWithdrawal()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(50m, 90, "int_A"), Payment(100m, 5, "int_B")];
    var approved = await h.Service.Approve(w.Principal.Id);
    approved.IsSuccess().Should().BeTrue();
    return (h, w);
  }

  [Fact]
  public async Task Settle_last_fragment_completes_and_books_the_paynow_identical_ledger()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    var id = w.Principal.Id;

    var first = await h.Service.SettleRefundFragment(
      id,
      $"{id}-1-0",
      h.Refunds.Fragments[0].AirwallexRefundId!,
      1
    );
    first.IsSuccess().Should().BeTrue();
    h.Wallet.WithdrawCalls.Should().Be(0, "money is only collected when ALL fragments settled");
    h.Repo.StatusWrites.Should().NotContain(s => s.Status == WithdrawStatus.Completed);

    var second = await h.Service.SettleRefundFragment(
      id,
      $"{id}-1-1",
      h.Refunds.Fragments[1].AirwallexRefundId!,
      1
    );

    second.IsSuccess().Should().BeTrue();
    h.Wallet.WithdrawCalls.Should().Be(1);
    h.Wallet.LastWithdrawAmount.Should().Be(Amount, "the full reserved amount is collected");
    h.Txn.Records.Should().HaveCount(2, "settlement books the same 2 ledger rows as PayNow");
    var settleRows = h.Txn.Records;
    settleRows[0].Type.Should().Be(TransactionType.WithdrawComplete);
    settleRows[0].Amount.Should().Be(Net);
    settleRows[1].Type.Should().Be(TransactionType.WithdrawFee);
    settleRows[1].Amount.Should().Be(Fee);
    settleRows[1].To.Should().Be(Accounts.WithdrawalFee.DisplayName);
    h.Repo.StatusWrites.Should().ContainSingle(s => s.Status == WithdrawStatus.Completed);
    h.Refunds.Fragments.Should().OnlyContain(f => f.Status == RefundFragmentStatus.Settled);
    h.Refunds.Fragments.Should().OnlyContain(f => f.SettledAt != null);
  }

  [Fact]
  public async Task Settle_redelivered_event_acks_without_moving_money_again()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    var id = w.Principal.Id;
    (await h.Service.SettleRefundFragment(id, $"{id}-1-0", "rf_0", 1)).IsSuccess().Should().BeTrue();
    (await h.Service.SettleRefundFragment(id, $"{id}-1-1", "rf_1", 1)).IsSuccess().Should().BeTrue();
    h.Wallet.WithdrawCalls.Should().Be(1);

    var redelivered = await h.Service.SettleRefundFragment(id, $"{id}-1-1", "rf_1", 1);

    redelivered.IsSuccess().Should().BeTrue("gateways redeliver webhooks at least once");
    h.Wallet.WithdrawCalls.Should().Be(1, "the reserve is collected exactly once");
    h.Txn.Records.Should().HaveCount(2);
  }

  [Fact]
  public async Task Settle_for_superseded_attempt_records_evidence_but_moves_no_money()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    var id = w.Principal.Id;
    // supersede: pretend a second attempt is now live
    h.Repo.MutateState(WithdrawStatus.Processing, Claimed(attempt: 2));

    var result = await h.Service.SettleRefundFragment(id, $"{id}-1-0", "rf_stale", 1);

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<StalePayoutEventException>();
    h.Wallet.WithdrawCalls.Should().Be(0);
    // the settlement is real money and must be recorded regardless
    h.Refunds.Fragments.First(f => f.RequestId == $"{id}-1-0")
      .Status.Should()
      .Be(RefundFragmentStatus.Settled, "evidence survives staleness fencing");
  }

  [Fact]
  public async Task Settle_of_unknown_fragment_is_stale_not_a_crash()
  {
    var (h, w) = await ApprovedCardWithdrawal();

    var result = await h.Service.SettleRefundFragment(
      w.Principal.Id,
      $"{w.Principal.Id}-9-7",
      "rf_x",
      9
    );

    result.IsSuccess().Should().BeFalse();
    result.FailureOrDefault().Should().BeOfType<StalePayoutEventException>();
    h.Wallet.WithdrawCalls.Should().Be(0);
  }

  // ---- FailRefundFragment (webhook failure) ----

  [Fact]
  public async Task Failed_fragment_parks_the_withdrawal_not_pending()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    var id = w.Principal.Id;
    // sibling settled first: money has partially left
    (await h.Service.SettleRefundFragment(id, $"{id}-1-0", "rf_0", 1)).IsSuccess().Should().BeTrue();

    var result = await h.Service.FailRefundFragment(id, $"{id}-1-1", "rf_1", "card expired", 1);

    result.IsSuccess().Should().BeTrue();
    h.Repo.StatusWrites
      .Should()
      .ContainSingle(s => s.Status == WithdrawStatus.RequireManualIntervention);
    h.Repo.StatusWrites
      .Should()
      .NotContain(
        s => s.Status == WithdrawStatus.Pending,
        "returning to Pending would let a fresh attempt double-refund the settled sibling"
      );
    h.Wallet.WithdrawCalls.Should().Be(0);
    h.Wallet.CancelWithdrawCalls.Should().Be(0, "no automatic refund of the reserve either");
    // evidence: one settled, one failed
    h.Refunds.Fragments.First(f => f.RequestId == $"{id}-1-0")
      .Status.Should()
      .Be(RefundFragmentStatus.Settled);
    h.Refunds.Fragments.First(f => f.RequestId == $"{id}-1-1")
      .Status.Should()
      .Be(RefundFragmentStatus.Failed);
  }

  [Fact]
  public async Task Failed_fragment_redelivery_acks_idempotently()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    var id = w.Principal.Id;
    (await h.Service.FailRefundFragment(id, $"{id}-1-0", "rf_0", "declined", 1))
      .IsSuccess()
      .Should()
      .BeTrue();

    var redelivered = await h.Service.FailRefundFragment(id, $"{id}-1-0", "rf_0", "declined", 1);

    redelivered.IsSuccess().Should().BeTrue();
    h.Repo.StatusWrites
      .Should()
      .ContainSingle(s => s.Status == WithdrawStatus.RequireManualIntervention);
  }

  // ---- Requeue (money safety on partial settlement) ----

  [Fact]
  public async Task Requeue_with_settled_fragments_is_refused()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    var id = w.Principal.Id;
    (await h.Service.SettleRefundFragment(id, $"{id}-1-0", "rf_0", 1)).IsSuccess().Should().BeTrue();
    (await h.Service.FailRefundFragment(id, $"{id}-1-1", "rf_1", "declined", 1))
      .IsSuccess()
      .Should()
      .BeTrue();

    var result = await h.Service.Requeue(id);

    result.IsSuccess().Should().BeFalse(
      "money already reached the card via the settled fragment; a fresh attempt would re-plan the full net and pay it twice"
    );
    result.FailureOrDefault().Should().BeOfType<InvalidWithdrawalOperationException>();
    h.Repo.StatusWrites.Should().NotContain(s => s.Status == WithdrawStatus.Pending);
  }

  [Fact]
  public async Task Requeue_with_only_failed_fragments_returns_to_pending()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    var id = w.Principal.Id;
    // both fragments failed, at the gateway and in zinc
    foreach (var idx in new[] { 0, 1 })
    {
      h.Gateway.MarkFailed($"{id}-1-{idx}");
      (await h.Service.FailRefundFragment(id, $"{id}-1-{idx}", $"rf_{idx}", "declined", 1))
        .IsSuccess()
        .Should()
        .BeTrue();
    }

    var result = await h.Service.Requeue(id);

    result.IsSuccess().Should().BeTrue("no money left, a fresh automated attempt is safe");
    h.Repo.StatusWrites.Should().Contain(s => s.Status == WithdrawStatus.Pending);
  }

  [Fact]
  public async Task Failed_fragment_frees_its_claim_on_the_refundable_pool()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    var id = w.Principal.Id;
    h.Gateway.MarkFailed($"{id}-1-0");
    (await h.Service.FailRefundFragment(id, $"{id}-1-0", "rf_0", "declined", 1))
      .IsSuccess()
      .Should()
      .BeTrue();

    // pool = 150 captured − 46 still-active fragment (int_B) = 104; the
    // failed 50 fragment (int_A) no longer counts
    var pool = await h.Service.RefundablePool("user-1");
    pool.SuccessOrDefault().Should().Be(150m - (Net - 50m));
  }

  // ---- Refundable pool vs the gateway's own refund record ----
  //
  // Before the card-refund rail existed, admins paid PayNow withdrawals out
  // by refunding the user's card by hand on the Airwallex dashboard. Those
  // refunds exist ONLY at the gateway, so a pool built from zinc's fragment
  // table alone planned new fragments against intents that were already
  // refunded — refunds the gateway can never create.

  private static GatewayRefund HandRefund(
    string intent,
    decimal amount,
    DateTime? createdAt = null,
    PayoutOutcome outcome = PayoutOutcome.Settled
  ) =>
    new()
    {
      Id = $"rfd_{Guid.NewGuid():N}",
      PaymentIntentId = intent,
      Amount = amount,
      Outcome = outcome,
      AcquirerReferenceNumber = null,
      CreatedAt = createdAt ?? DateTime.UtcNow.AddDays(-30),
      UpdatedAt = createdAt ?? DateTime.UtcNow.AddDays(-30),
      // a dashboard refund carries a random request id zinc never chose
      RequestId = Guid.NewGuid().ToString(),
    };

  private static WithdrawalRefundFragment Fragment(
    FundingPayment p,
    decimal amount,
    string requestId,
    string? refundId = null,
    RefundFragmentStatus status = RefundFragmentStatus.Created
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      WithdrawalId = Guid.NewGuid(),
      PaymentId = p.PaymentId,
      PaymentIntentId = p.PaymentIntentId,
      AirwallexRefundId = refundId,
      RequestId = requestId,
      Amount = amount,
      Status = status,
      CreatedAt = DateTime.UtcNow,
      SettledAt = null,
    };

  private static Dictionary<string, List<GatewayRefund>> ByIntent(
    params GatewayRefund[] refunds
  ) => refunds.GroupBy(r => r.PaymentIntentId).ToDictionary(g => g.Key, g => g.ToList());

  [Fact]
  public void RefundPool_excludes_an_intent_fully_refunded_by_hand()
  {
    var refunded = Payment(10m, 90, "int_sgpdtw8p7hj3aotuvlt");
    var live = Payment(40m, 10, "int_live");

    var pool = RefundPool.Compute(
      [refunded, live],
      [],
      ByIntent(HandRefund("int_sgpdtw8p7hj3aotuvlt", 10m))
    );

    pool.Should().ContainSingle().Which.PaymentIntentId.Should().Be("int_live");
    pool[0].Refundable.Should().Be(40m);
  }

  [Fact]
  public void RefundPool_reduces_a_partially_hand_refunded_intent()
  {
    var p = Payment(100m, 30, "int_A");

    var pool = RefundPool.Compute([p], [], ByIntent(HandRefund("int_A", 70m)));

    pool.Should().ContainSingle().Which.Refundable.Should().Be(30m);
  }

  [Fact]
  public void RefundPool_does_not_double_count_a_fragment_the_gateway_lists_by_refund_id()
  {
    var p = Payment(100m, 30, "int_A");
    var ours = Fragment(p, 25m, "w-1-0", refundId: "rf_ours");
    var atGateway = HandRefund("int_A", 25m) with { Id = "rf_ours", RequestId = "w-1-0" };

    var pool = RefundPool.Compute([p], [ours], ByIntent(atGateway));

    pool.Should().ContainSingle().Which.Refundable.Should().Be(75m, "25 counted once, not twice");
  }

  [Fact]
  public void RefundPool_does_not_double_count_a_fragment_whose_create_response_was_lost()
  {
    // the gateway created the refund but zinc never stored its id: the
    // request id is the only link
    var p = Payment(100m, 30, "int_A");
    var ours = Fragment(p, 25m, "w-1-0");
    var atGateway = HandRefund("int_A", 25m) with { RequestId = "w-1-0" };

    var pool = RefundPool.Compute([p], [ours], ByIntent(atGateway));

    pool.Should().ContainSingle().Which.Refundable.Should().Be(75m);
  }

  [Fact]
  public void RefundPool_adds_fragments_not_yet_at_the_gateway_on_top_of_hand_refunds()
  {
    var p = Payment(100m, 30, "int_A");
    var planned = Fragment(p, 20m, "w-1-0"); // persisted, not sent yet

    var pool = RefundPool.Compute([p], [planned], ByIntent(HandRefund("int_A", 50m)));

    pool.Should().ContainSingle().Which.Refundable.Should().Be(30m);
  }

  [Fact]
  public void RefundPool_ignores_failed_gateway_refunds_and_failed_fragments()
  {
    var p = Payment(100m, 30, "int_A");
    var failedFragment = Fragment(p, 40m, "w-1-0", status: RefundFragmentStatus.Failed);

    var pool = RefundPool.Compute(
      [p],
      [failedFragment],
      ByIntent(HandRefund("int_A", 60m, outcome: PayoutOutcome.Failed))
    );

    pool.Should().ContainSingle().Which.Refundable.Should().Be(100m);
  }

  [Fact]
  public void RefundPool_matching_a_FAILED_gateway_refund_keeps_the_fragment_claim()
  {
    // zinc has not recorded the failure yet: the fragment still claims its
    // amount (errs towards a smaller pool, never a larger one)
    var p = Payment(100m, 30, "int_A");
    var ours = Fragment(p, 25m, "w-1-0", refundId: "rf_ours");
    var failed = HandRefund("int_A", 25m, outcome: PayoutOutcome.Failed) with
    {
      Id = "rf_ours",
      RequestId = "w-1-0",
    };

    var pool = RefundPool.Compute([p], [ours], ByIntent(failed));

    pool.Should().ContainSingle().Which.Refundable.Should().Be(75m);
  }

  [Fact]
  public async Task Pool_reads_every_intent_from_the_gateway()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(50m, 90, "int_A"), Payment(100m, 5, "int_B")];
    h.Gateway.Refunds.Add(HandRefund("int_A", 50m));

    var pool = await h.Service.RefundablePool("user-1");

    pool.SuccessOrDefault().Should().Be(100m);
    h.Gateway.ListedIntents.Should().BeEquivalentTo(["int_A", "int_B"]);
  }

  [Fact]
  public async Task Pool_fails_when_the_gateway_cannot_be_asked()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(200m, 10, "int_A")];
    h.Gateway.ListFailing = true;

    var pool = await h.Service.RefundablePool("user-1");

    pool.IsSuccess().Should().BeFalse("never fall back to zinc's partial view of the refunds");
    pool.FailureOrDefault().Should().BeOfType<RefundGatewayUnavailableException>();
  }

  [Fact]
  public async Task Create_card_refund_fails_without_reserving_when_the_gateway_is_unreachable()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(200m, 10, "int_A")];
    h.Gateway.ListFailing = true;

    var result = await h.Service.Create(
      "user-1",
      new WithdrawalRecord
      {
        Amount = Amount,
        Method = WithdrawalMethod.CardRefund,
        PayNowNumber = null,
      }
    );

    result.FailureOrDefault().Should().BeOfType<RefundGatewayUnavailableException>();
    h.Wallet.PrepareWithdrawCalls.Should().Be(0);
  }

  [Fact]
  public async Task Approve_plans_around_an_intent_already_refunded_by_hand()
  {
    // the production failure: int_A was refunded in full on the dashboard,
    // so the whole net must come from int_B
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(50m, 90, "int_A"), Payment(100m, 5, "int_B")];
    h.Gateway.Refunds.Add(HandRefund("int_A", 50m));

    var result = await h.Service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    h.Gateway.Requests.Should().ContainSingle();
    h.Gateway.Requests[0].PaymentIntentId.Should().Be("int_B");
    h.Gateway.Requests[0].Amount.Should().Be(Net);
  }

  [Fact]
  public async Task Approve_with_hand_refunds_shrinking_the_pool_reverts_to_pending()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(50m, 90, "int_A"), Payment(100m, 5, "int_B")];
    h.Gateway.Refunds.Add(HandRefund("int_B", 100m));

    var result = await h.Service.Approve(w.Principal.Id);

    result.FailureOrDefault().Should().BeOfType<InsufficientRefundablePoolException>();
    h.Repo.StatusWrites.Select(s => s.Status)
      .Should()
      .ContainInOrder(WithdrawStatus.Processing, WithdrawStatus.Pending);
    h.Gateway.Requests.Should().BeEmpty();
  }

  [Fact]
  public async Task Approve_with_the_gateway_unreachable_reverts_to_pending_without_refunds()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(200m, 10, "int_A")];
    h.Gateway.ListFailing = true;

    var result = await h.Service.Approve(w.Principal.Id);

    result.FailureOrDefault().Should().BeOfType<RefundGatewayUnavailableException>();
    h.Repo.StatusWrites.Select(s => s.Status)
      .Should()
      .ContainInOrder(WithdrawStatus.Processing, WithdrawStatus.Pending);
    h.Refunds.Fragments.Should().BeEmpty();
    h.Gateway.Requests.Should().BeEmpty();
  }

  // ---- Refund create rejection is surfaced on the fragment ----

  [Fact]
  public async Task Rejected_refund_create_records_the_gateway_error_on_the_fragment()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(50m, 90, "int_A"), Payment(100m, 5, "int_B")];
    h.Gateway.FailFromRequest = 1;

    var result = await h.Service.Approve(w.Principal.Id);

    result.IsSuccess().Should().BeFalse();
    h.Refunds.Fragments[0].LastError.Should().BeNull();
    h.Refunds.Fragments[1]
      .LastError.Should()
      .Be("Airwallex rejected the refund (HTTP 400, x)", "only the controlled diagnostic");
    h.Refunds.Fragments[1].LastError.Should().NotContain("raw body");
    h.Refunds.Fragments[1].Status.Should().Be(RefundFragmentStatus.Created);
  }

  [Fact]
  public async Task Unclassified_create_failure_records_a_fixed_message_not_the_exception_text()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(200m, 10, "int_A")];
    h.Gateway.FailFromRequest = 0;
    h.Gateway.CreateFailure = new InvalidOperationException("internal detail");

    (await h.Service.Approve(w.Principal.Id)).IsSuccess().Should().BeFalse();

    h.Refunds.Fragments[0].LastError.Should().Be("Airwallex refund creation failed (see API logs)");
  }

  [Fact]
  public async Task Successful_redrive_clears_the_recorded_create_error()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(50m, 90, "int_A"), Payment(100m, 5, "int_B")];
    h.Gateway.FailFromRequest = 1;
    (await h.Service.Approve(w.Principal.Id)).IsSuccess().Should().BeFalse();

    h.Gateway.FailFromRequest = null;
    (await h.Service.Approve(w.Principal.Id)).IsSuccess().Should().BeTrue();

    h.Refunds.Fragments.Should().OnlyContain(f => f.LastError == null);
  }

  // ---- Requeue vs hand refunds and dead fragments ----

  // A card withdrawal parked after its attempt-1 refund creates were rejected:
  // its fragments sit Created with no refund id.
  private async Task<(Harness H, Withdrawal W)> ParkedRejectedCardWithdrawal()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);
    h.Refunds.FundingPayments = [Payment(50m, 90, "int_A"), Payment(100m, 5, "int_B")];
    h.Gateway.FailFromRequest = 0;
    (await h.Service.Approve(w.Principal.Id)).IsSuccess().Should().BeFalse();
    h.Gateway.FailFromRequest = null;
    h.Repo.MutateState(WithdrawStatus.RequireManualIntervention, h.Repo.LastPayoutWritten);
    return (h, w);
  }

  [Fact]
  public async Task Requeue_releases_dead_fragments_so_they_stop_claiming_the_pool()
  {
    var (h, w) = await ParkedRejectedCardWithdrawal();
    (await h.Service.RefundablePool("user-1"))
      .SuccessOrDefault()
      .Should()
      .Be(150m - Net, "the dead slices still claim the pool before requeue");

    var result = await h.Service.Requeue(w.Principal.Id);

    result.IsSuccess().Should().BeTrue();
    h.Repo.StatusWrites.Should().Contain(s => s.Status == WithdrawStatus.Pending);
    h.Refunds.Fragments.Should().OnlyContain(f => f.Status == RefundFragmentStatus.Failed);
    (await h.Service.RefundablePool("user-1")).SuccessOrDefault().Should().Be(150m);
  }

  [Fact]
  public async Task Requeue_is_refused_when_a_card_was_refunded_by_hand_after_the_withdrawal()
  {
    // withdrawal 0ded0290: a slice's card was refunded by hand on the
    // dashboard after the withdrawal was made; a re-plan would take the full
    // net again from the other cards and pay that part twice
    var (h, w) = await ParkedRejectedCardWithdrawal();
    h.Gateway.Refunds.Add(HandRefund("int_B", 46m, createdAt: DateTime.UtcNow.AddMinutes(5)));

    var result = await h.Service.Requeue(w.Principal.Id);

    result.FailureOrDefault().Should().BeOfType<InvalidWithdrawalOperationException>();
    result.FailureOrDefault().Message.Should().Contain("by hand");
    h.Repo.StatusWrites.Should().NotContain(s => s.Status == WithdrawStatus.Pending);
    h.Refunds.Fragments.Should().OnlyContain(f => f.Status == RefundFragmentStatus.Created);
  }

  [Fact]
  public async Task Requeue_tolerates_hand_refunds_older_than_the_withdrawal()
  {
    // PayNow-era refunds predate this withdrawal, so they cannot be its
    // payout; the pool already accounts for them on the next approve
    var (h, w) = await ParkedRejectedCardWithdrawal();
    h.Gateway.Refunds.Add(HandRefund("int_A", 50m, createdAt: DateTime.UtcNow.AddDays(-60)));

    var result = await h.Service.Requeue(w.Principal.Id);

    result.IsSuccess().Should().BeTrue();
  }

  [Fact]
  public async Task Requeue_is_refused_while_a_fragment_is_live_at_the_gateway()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    h.Repo.MutateState(WithdrawStatus.RequireManualIntervention, h.Repo.LastPayoutWritten);

    var result = await h.Service.Requeue(w.Principal.Id);

    result.FailureOrDefault().Should().BeOfType<InvalidWithdrawalOperationException>();
    h.Repo.StatusWrites.Should().NotContain(s => s.Status == WithdrawStatus.Pending);
  }

  [Fact]
  public async Task Requeue_is_refused_when_the_gateway_cannot_be_asked()
  {
    var (h, w) = await ParkedRejectedCardWithdrawal();
    h.Gateway.ListFailing = true;

    var result = await h.Service.Requeue(w.Principal.Id);

    result.FailureOrDefault().Should().BeOfType<RefundGatewayUnavailableException>();
    h.Repo.StatusWrites.Should().NotContain(s => s.Status == WithdrawStatus.Pending);
  }

  // ---- Manual complete (receipt upload) of a parked card withdrawal ----

  private static Task<Result<WithdrawalPrincipal>> CompleteByHand(Harness h, Withdrawal w) =>
    h.Service.Complete(w.Principal.Id, "admin-1", "hand refund + PayNow", new MemoryStream([1]));

  [Fact]
  public async Task Manual_complete_from_rmi_releases_dead_fragments_and_collects_the_reserve()
  {
    // withdrawal 0ded0290: part of the net was refunded by hand on the
    // dashboard, the rest sent by PayNow; the admin closes it with a receipt
    var (h, w) = await ParkedRejectedCardWithdrawal();
    h.Gateway.Refunds.Add(HandRefund("int_B", 46m, createdAt: DateTime.UtcNow.AddMinutes(5)));
    var fee = h.Repo.LastPayoutWritten!.Fee;

    var result = await CompleteByHand(h, w);

    result.IsSuccess().Should().BeTrue();
    h.Repo.StatusWrites.Should().Contain(s => s.Status == WithdrawStatus.Completed);
    h.Refunds.Fragments.Should().OnlyContain(f => f.Status == RefundFragmentStatus.Failed);
    h.Wallet.WithdrawCalls.Should().Be(1);
    h.Wallet.LastWithdrawAmount.Should().Be(Amount);
    h.Txn.Records.Should().HaveCount(2);
    h.Txn.Records[0].Amount.Should().Be(Amount - fee);
    h.Txn.Records[1].Amount.Should().Be(fee, "the payout's recorded fee is collected");
    h.Repo.LastPayoutWritten!.Fee.Should().Be(fee);
    h.Repo.LastCompleteWritten!.CompleterId.Should().Be("admin-1");
    h.Repo.LastCompleteWritten!.Receipt.Should().Be("receipt-key");
  }

  [Fact]
  public async Task Manual_complete_from_rmi_keeps_settled_fragments_settled()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    var id = w.Principal.Id;
    (await h.Service.SettleRefundFragment(id, $"{id}-1-0", "rf_0", 1)).IsSuccess().Should().BeTrue();
    h.Gateway.MarkFailed($"{id}-1-1");
    (await h.Service.FailRefundFragment(id, $"{id}-1-1", "rf_1", "declined", 1))
      .IsSuccess()
      .Should()
      .BeTrue();

    var result = await CompleteByHand(h, w);

    result.IsSuccess().Should().BeTrue();
    h.Refunds.Fragments.Single(f => f.RequestId == $"{id}-1-0")
      .Status.Should()
      .Be(RefundFragmentStatus.Settled);
    h.Refunds.Fragments.Single(f => f.RequestId == $"{id}-1-1")
      .Status.Should()
      .Be(RefundFragmentStatus.Failed);
  }

  [Fact]
  public async Task Manual_complete_from_rmi_is_refused_while_a_fragment_is_live_at_the_gateway()
  {
    var (h, w) = await ApprovedCardWithdrawal();
    h.Repo.MutateState(WithdrawStatus.RequireManualIntervention, h.Repo.LastPayoutWritten);

    var result = await CompleteByHand(h, w);

    result.FailureOrDefault().Should().BeOfType<InvalidWithdrawalOperationException>();
    result.FailureOrDefault().Message.Should().Contain("still be moving");
    h.Repo.StatusWrites.Should().NotContain(s => s.Status == WithdrawStatus.Completed);
    h.Refunds.Fragments.Should().OnlyContain(f => f.Status == RefundFragmentStatus.Created);
    h.Wallet.WithdrawCalls.Should().Be(0);
    h.Txn.Records.Should().BeEmpty();
  }

  [Fact]
  public async Task Manual_complete_from_rmi_is_refused_when_the_gateway_cannot_be_asked()
  {
    var (h, w) = await ParkedRejectedCardWithdrawal();
    h.Gateway.ListFailing = true;

    var result = await CompleteByHand(h, w);

    result.FailureOrDefault().Should().BeOfType<RefundGatewayUnavailableException>();
    h.Repo.StatusWrites.Should().NotContain(s => s.Status == WithdrawStatus.Completed);
    h.Refunds.Fragments.Should().OnlyContain(f => f.Status == RefundFragmentStatus.Created);
    h.Wallet.WithdrawCalls.Should().Be(0);
  }

  [Fact]
  public async Task Manual_complete_of_pending_card_withdrawal_does_not_ask_the_gateway()
  {
    var w = WithdrawalWith(WithdrawStatus.Pending);
    var h = Make(w);

    var result = await CompleteByHand(h, w);

    result.IsSuccess().Should().BeTrue();
    h.Gateway.ListedIntents.Should().BeEmpty();
    h.Txn.Records[1].Amount.Should().Be(Fee, "no payout yet, so the fee is computed as today");
  }

  // ---- fakes ----

  private sealed class PassThroughTransactionManager : ITransactionManager
  {
    public Task<Result<T>> Start<T>(Func<Task<Result<T>>> func) => func();
  }

  private sealed class FourPercentFeeCalculator : IFeeCalculator
  {
    public Task<Result<FeeSpec>> Current(FeeType type) =>
      Task.FromResult<Result<FeeSpec>>(new FeeSpec { Percentage = 4m, FlatAmount = 0m });

    public Task<Result<decimal>> Compute(FeeType type, decimal amount) =>
      Task.FromResult<Result<decimal>>(Math.Round(amount * 0.04m, 2, MidpointRounding.ToEven));
  }

  private sealed class FakeWithdrawalStorage : IWithdrawalStorage
  {
    public Task<Result<string>> Save(Stream stream) =>
      Task.FromResult((Result<string>)"receipt-key");

    public Task<Result<string>> Get(string key) => Task.FromResult((Result<string>)"url");

    public Task<Result<string>> Get(string key, TimeSpan expiry) =>
      Task.FromResult((Result<string>)"url");
  }

  private sealed class FakeRefundGateway : IRefundGateway
  {
    public List<RefundRequest> Requests { get; } = [];

    // fail every request whose ordinal (0-based, across the whole test) is
    // >= this value; null = all succeed
    public int? FailFromRequest { get; set; }

    private int counter;

    // the gateway's own refund record, per intent: every refund this fake
    // created, plus refunds seeded by a test (e.g. issued by hand on the
    // dashboard, which zinc never sees)
    public List<GatewayRefund> Refunds { get; } = [];

    // what a failed create returns; the real client always returns a
    // RefundCreateFailedException carrying a controlled diagnostic
    public Exception CreateFailure { get; set; } =
      new RefundCreateFailedException(
        "Airwallex refund creation failed (400): {\"code\":\"x\",\"secret\":\"raw body\"}",
        "Airwallex rejected the refund (HTTP 400, x)"
      );

    // fail every per-intent refund listing (gateway unreachable)
    public bool ListFailing { get; set; }

    public List<string> ListedIntents { get; } = [];

    public Task<Result<RefundConfirmation>> CreateRefund(RefundRequest request)
    {
      var ordinal = this.counter++;
      Requests.Add(request);
      if (FailFromRequest != null && ordinal >= FailFromRequest)
        return Task.FromResult<Result<RefundConfirmation>>(CreateFailure);
      var id = $"rf_{request.RequestId}";
      Refunds.Add(
        new GatewayRefund
        {
          Id = id,
          PaymentIntentId = request.PaymentIntentId,
          Amount = request.Amount,
          Outcome = PayoutOutcome.InFlight,
          AcquirerReferenceNumber = null,
          CreatedAt = DateTime.UtcNow,
          UpdatedAt = DateTime.UtcNow,
          RequestId = request.RequestId,
        }
      );
      return Task.FromResult<Result<RefundConfirmation>>(new RefundConfirmation { Id = id });
    }

    // what the gateway reports once a refund failed (the webhook that
    // FailRefundFragment models carries the same fact)
    public void MarkFailed(string requestId)
    {
      var idx = Refunds.FindIndex(r => r.RequestId == requestId);
      Refunds[idx] = Refunds[idx] with { Outcome = PayoutOutcome.Failed };
    }

    public Task<Result<List<GatewayRefund>>> ListRefundsByPaymentIntent(string paymentIntentId)
    {
      ListedIntents.Add(paymentIntentId);
      if (ListFailing)
        return Task.FromResult<Result<List<GatewayRefund>>>(
          new HttpRequestException("gateway timeout")
        );
      return Task.FromResult<Result<List<GatewayRefund>>>(
        Refunds.Where(r => r.PaymentIntentId == paymentIntentId).ToList()
      );
    }

    // reconciliation lookups are not exercised in this suite
    public Task<Result<RefundStatus>> GetRefundStatus(string refundId) =>
      throw new NotImplementedException();

    public Task<Result<List<GatewayRefund>>> ListRefunds(DateTime fromUtc, DateTime toUtc) =>
      throw new NotImplementedException();
  }

  private sealed class FakeRefundRepository : IWithdrawalRefundRepository
  {
    public List<FundingPayment> FundingPayments { get; set; } = [];
    public List<FundingPayment> OutOfWindowPayments { get; set; } = [];

    // pre-existing refunds against a payment (e.g. from other withdrawals)
    public Dictionary<Guid, decimal> RefundedByPayment { get; } = [];

    public List<WithdrawalRefundFragment> Fragments { get; } = [];

    public int PoolQueries { get; private set; }

    public Task<Result<List<FundingPayment>>> ListFundingPayments(Guid walletId, DateTime since)
    {
      PoolQueries++;
      var all = FundingPayments.Concat(OutOfWindowPayments);
      return Task.FromResult<Result<List<FundingPayment>>>(
        all.Where(p => p.CreatedAt >= since).OrderBy(p => p.CreatedAt).ToList()
      );
    }

    public Task<Result<List<WithdrawalRefundFragment>>> ListActiveRefundsByPayment(
      IEnumerable<Guid> paymentIds
    )
    {
      var ids = paymentIds.ToHashSet();
      // RefundedByPayment models other withdrawals' fragments that are not
      // at the gateway yet (no refund id): they count in full
      var others = RefundedByPayment
        .Where(x => ids.Contains(x.Key))
        .Select(x => new WithdrawalRefundFragment
        {
          Id = Guid.NewGuid(),
          WithdrawalId = Guid.NewGuid(),
          PaymentId = x.Key,
          PaymentIntentId = "int_other",
          AirwallexRefundId = null,
          RequestId = $"other-{x.Key}",
          Amount = x.Value,
          Status = RefundFragmentStatus.Created,
          CreatedAt = DateTime.UtcNow,
          SettledAt = null,
        });
      return Task.FromResult<Result<List<WithdrawalRefundFragment>>>(
        Fragments
          .Where(f => ids.Contains(f.PaymentId) && f.Status != RefundFragmentStatus.Failed)
          .Concat(others)
          .ToList()
      );
    }

    public Task<Result<WithdrawalRefundFragment?>> RecordCreateError(Guid id, string error)
    {
      var idx = Fragments.FindIndex(f => f.Id == id);
      if (idx < 0)
        return Task.FromResult<Result<WithdrawalRefundFragment?>>(
          (WithdrawalRefundFragment?)null
        );
      Fragments[idx] = Fragments[idx] with { LastError = error };
      return Task.FromResult<Result<WithdrawalRefundFragment?>>(Fragments[idx]);
    }

    public Task<Result<List<WithdrawalRefundFragment>>> ListByWithdrawal(Guid withdrawalId) =>
      Task.FromResult<Result<List<WithdrawalRefundFragment>>>(
        Fragments
          .Where(f => f.WithdrawalId == withdrawalId)
          .OrderBy(f => f.RequestId, StringComparer.Ordinal)
          .ToList()
      );

    public Task<Result<WithdrawalRefundFragment?>> GetByRequestId(string requestId) =>
      Task.FromResult<Result<WithdrawalRefundFragment?>>(
        Fragments.FirstOrDefault(f => f.RequestId == requestId)
      );

    public Task<Result<List<WithdrawalRefundFragment>>> CreateMany(
      IEnumerable<WithdrawalRefundFragment> fragments
    )
    {
      var list = fragments.ToList();
      Fragments.AddRange(list);
      return Task.FromResult<Result<List<WithdrawalRefundFragment>>>(list);
    }

    public Task<Result<List<WithdrawalRefundFragment>>> ListSettledMissingArn(
      DateTime createdOnOrAfter,
      IEnumerable<Guid> excludeIds,
      int max
    )
    {
      var excluded = excludeIds.ToHashSet();
      return Task.FromResult<Result<List<WithdrawalRefundFragment>>>(
        Fragments
          .Where(f =>
            f.Status == RefundFragmentStatus.Settled
            && f.AirwallexRefundId != null
            && f.AcquirerReferenceNumber == null
            && f.CreatedAt >= createdOnOrAfter
            && !excluded.Contains(f.Id)
          )
          .OrderBy(f => f.CreatedAt)
          .Take(max)
          .ToList()
      );
    }

    public Task<Result<List<WithdrawalRefundFragment>>> ListByAirwallexRefundIds(
      IEnumerable<string> refundIds
    )
    {
      var ids = refundIds.ToHashSet(StringComparer.Ordinal);
      return Task.FromResult<Result<List<WithdrawalRefundFragment>>>(
        Fragments
          .Where(f => f.AirwallexRefundId != null && ids.Contains(f.AirwallexRefundId))
          .ToList()
      );
    }

    public Task<Result<List<PaymentIntentOwner>>> ListPaymentIntentOwners(
      IEnumerable<string> paymentIntentIds
    ) => throw new NotImplementedException();

    public Task<Result<List<WithdrawalCandidate>>> ListCandidatesByWallets(
      IEnumerable<Guid> walletIds
    ) => throw new NotImplementedException();

    public Task<Result<int>> CountUnbackfillableArn(DateTime createdBefore) =>
      Task.FromResult<Result<int>>(
        Fragments.Count(f =>
          f.Status == RefundFragmentStatus.Settled
          && f.AirwallexRefundId != null
          && f.AcquirerReferenceNumber == null
          && f.CreatedAt < createdBefore
        )
      );

    public Task<Result<WithdrawalRefundFragment?>> Update(
      Guid id,
      RefundFragmentStatus? status,
      string? airwallexRefundId,
      DateTime? settledAt,
      string? acquirerReferenceNumber
    )
    {
      var idx = Fragments.FindIndex(f => f.Id == id);
      if (idx < 0)
        return Task.FromResult<Result<WithdrawalRefundFragment?>>(
          (WithdrawalRefundFragment?)null
        );
      var updated = Fragments[idx] with
      {
        Status = status ?? Fragments[idx].Status,
        AirwallexRefundId = airwallexRefundId ?? Fragments[idx].AirwallexRefundId,
        SettledAt = settledAt ?? Fragments[idx].SettledAt,
        AcquirerReferenceNumber =
          acquirerReferenceNumber ?? Fragments[idx].AcquirerReferenceNumber,
        LastError = airwallexRefundId != null ? null : Fragments[idx].LastError,
      };
      Fragments[idx] = updated;
      return Task.FromResult<Result<WithdrawalRefundFragment?>>(updated);
    }
  }

  private sealed class FakePayoutGateway : IPayoutGateway
  {
    public List<PayoutRequest> Requests { get; } = [];

    public Task<Result<PayoutConfirmation>> CreatePayout(PayoutRequest request)
    {
      Requests.Add(request);
      return Task.FromResult<Result<PayoutConfirmation>>(
        new PayoutConfirmation { Id = "transfer-1" }
      );
    }

    public Task<Result<PayoutStatus>> GetPayoutStatus(
      string requestId,
      string? confirmationNumber
    ) => throw new NotImplementedException();
  }

  private sealed class FakeWithdrawalRepository(Withdrawal? withdrawal) : IWithdrawalRepository
  {
    private WithdrawStatus? current;
    private WithdrawalPayout? currentPayout = withdrawal?.Principal.Payout;
    private bool payoutTouched;

    public List<WithdrawalStatus> StatusWrites { get; } = [];
    public WithdrawalPayout? LastPayoutWritten { get; private set; }
    public WithdrawalComplete? LastCompleteWritten { get; private set; }

    public void MutateState(WithdrawStatus status, WithdrawalPayout? payout)
    {
      current = status;
      currentPayout = payout;
      payoutTouched = true;
      LastPayoutWritten = payout;
    }

    public Task<Result<Withdrawal?>> Get(Guid id, string? userId)
    {
      if (withdrawal == null)
        return Task.FromResult((Result<Withdrawal?>)(Withdrawal?)null);
      var status =
        current == null
          ? withdrawal.Principal.Status
          : withdrawal.Principal.Status with
          {
            Status = current.Value,
          };
      var w = withdrawal with
      {
        Principal = withdrawal.Principal with
        {
          Status = status,
          Payout = payoutTouched ? currentPayout : withdrawal.Principal.Payout,
        },
      };
      return Task.FromResult((Result<Withdrawal?>)w);
    }

    public Task<Result<WithdrawalPrincipal?>> Update(
      string? userId,
      Guid id,
      WithdrawalRecord? record,
      WithdrawalStatus? status,
      WithdrawalComplete? complete,
      WithdrawalPayout? payout = null
    )
    {
      if (status != null)
      {
        StatusWrites.Add(status);
        current = status.Status;
      }
      if (payout != null)
      {
        LastPayoutWritten = payout;
        currentPayout = payout;
        payoutTouched = true;
      }
      if (complete != null)
        LastCompleteWritten = complete;
      var principal = withdrawal!.Principal with
      {
        Status = status ?? withdrawal.Principal.Status,
        Payout = payout ?? currentPayout ?? withdrawal.Principal.Payout,
        Complete = complete ?? withdrawal.Principal.Complete,
      };
      return Task.FromResult((Result<WithdrawalPrincipal?>)principal);
    }

    public Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search) =>
      throw new NotImplementedException();

    public Task<Result<WithdrawalPrincipal>> Create(Guid walletId, WithdrawalRecord record) =>
      Task.FromResult(
        (Result<WithdrawalPrincipal>)(
          withdrawal!.Principal with
          {
            Record = record,
          }
        )
      );

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }

  private sealed class FakeWalletRepository(Withdrawal? withdrawal) : IWalletRepository
  {
    public int WithdrawCalls { get; private set; }
    public int CancelWithdrawCalls { get; private set; }
    public int PrepareWithdrawCalls { get; private set; }
    public decimal? LastWithdrawAmount { get; private set; }

    private static WalletPrincipal Wallet(Guid id) =>
      new()
      {
        Id = id,
        UserId = "user-1",
        Record = new WalletRecord
        {
          Usable = 200m,
          WithdrawReserve = 0m,
          BookingReserve = 0m,
        },
      };

    public Task<Result<Wallet?>> GetByUserId(string userId) =>
      Task.FromResult(
        (Result<Wallet?>)
          new Wallet
          {
            Principal = withdrawal?.Wallet ?? Wallet(WalletId),
            User = new UserPrincipal
            {
              Id = "user-1",
              Record = new UserRecord { Username = "tester" },
            },
          }
      );

    public Task<Result<WalletPrincipal?>> Withdraw(Guid id, decimal amount)
    {
      WithdrawCalls++;
      LastWithdrawAmount = amount;
      return Task.FromResult((Result<WalletPrincipal?>)Wallet(id));
    }

    public Task<Result<WalletPrincipal?>> CancelWithdraw(Guid id, decimal amount)
    {
      CancelWithdrawCalls++;
      return Task.FromResult((Result<WalletPrincipal?>)Wallet(id));
    }

    public Task<Result<WalletPrincipal?>> PrepareWithdraw(Guid id, decimal amount)
    {
      PrepareWithdrawCalls++;
      return Task.FromResult((Result<WalletPrincipal?>)Wallet(id));
    }

    public Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Wallet?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> BookStart(Guid id, decimal amount) =>
      throw new NotImplementedException();

    public Task<Result<WalletPrincipal?>> BookEnd(Guid id, decimal revert, decimal collect) =>
      throw new NotImplementedException();
  }

  private sealed class FakeTransactionRepository : ITransactionRepository
  {
    public List<TransactionRecord> Records { get; } = [];

    public Task<Result<TransactionPrincipal>> Create(
      Guid walletId,
      TransactionRecord record,
      Guid? paymentId = null
    )
    {
      Records.Add(record);
      var principal = new TransactionPrincipal
      {
        Id = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        Record = record,
      };
      return Task.FromResult((Result<TransactionPrincipal>)principal);
    }

    public Task<Result<IEnumerable<TransactionPrincipal>>> Search(TransactionSearch search) =>
      throw new NotImplementedException();

    public Task<Result<Transaction?>> Get(Guid id, string? userId) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }
}
