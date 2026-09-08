using CSharp_Result;
using Domain.Withdrawal;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Withdrawals;

// The runner wires the matcher to the gateway and the database. What matters
// here is the two-phase contract: Report writes NOTHING, Apply writes only the
// confidently-matched bucket, and both are safe to repeat.
public class RefundReconciliationRunnerTests
{
  private static readonly DateTime Now = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime To = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

  private static readonly Guid Wallet = Guid.NewGuid();
  private static readonly Guid PaymentId = Guid.NewGuid();
  private const string Intent = "int_funding";
  private const string User = "user-1";

  [Fact]
  public async Task The_dry_run_writes_nothing()
  {
    var repo = new FakeRepo();
    var withdrawal = repo.AddCandidate(amount: 100m);
    var gateway = new FakeGateway { Refunds = { Refund("rfd_1", 100m, From.AddDays(10)) } };

    var report = await Report(repo, gateway);

    report.Matched.Should().HaveCount(1);
    report.Matched[0].WithdrawalId.Should().Be(withdrawal.Id);
    repo.Fragments.Should().BeEmpty("a report must never write");
  }

  [Fact]
  public async Task Apply_attaches_a_fragment_for_a_confidently_matched_refund()
  {
    var repo = new FakeRepo();
    var withdrawal = repo.AddCandidate(amount: 100m);
    var gateway = new FakeGateway { Refunds = { Refund("rfd_1", 100m, From.AddDays(10)) } };

    var applied = await Apply(repo, gateway);

    applied.Eligible.Should().Be(1);
    applied.Attached.Should().Be(1);
    var fragment = repo.Fragments.Should().ContainSingle().Subject;
    fragment.WithdrawalId.Should().Be(withdrawal.Id);
    fragment.AirwallexRefundId.Should().Be("rfd_1");
    fragment.PaymentIntentId.Should().Be(Intent);
    fragment.PaymentId.Should().Be(PaymentId);
    fragment.Amount.Should().Be(100m);
    fragment.Status.Should().Be(RefundFragmentStatus.Settled);
    // the ARN is the whole point: it lands in the tax CSV
    fragment.AcquirerReferenceNumber.Should().Be("12345678901234567890123");
    // evidence of when the money MOVED, not of when we reconciled it
    fragment.CreatedAt.Should().Be(From.AddDays(10));
  }

  // The ambiguous bucket is never applied. This is the safety property the
  // whole two-phase design exists for.
  [Fact]
  public async Task Apply_never_attaches_an_ambiguous_refund()
  {
    var repo = new FakeRepo();
    repo.AddCandidate(amount: 100m, completedAt: From.AddDays(10));
    repo.AddCandidate(amount: 100m, completedAt: From.AddDays(12));
    var gateway = new FakeGateway { Refunds = { Refund("rfd_1", 100m, From.AddDays(11)) } };

    var report = await Report(repo, gateway);
    report.Ambiguous.Should().HaveCount(1);

    var applied = await Apply(repo, gateway);

    applied.Eligible.Should().Be(0);
    applied.Attached.Should().Be(0);
    repo.Fragments.Should().BeEmpty();
  }

  // Running the apply twice must not double-attach: the second pass sees the
  // fragment the first one wrote and skips the refund.
  [Fact]
  public async Task Apply_is_idempotent()
  {
    var repo = new FakeRepo();
    repo.AddCandidate(amount: 100m);
    var gateway = new FakeGateway { Refunds = { Refund("rfd_1", 100m, From.AddDays(10)) } };

    var first = await Apply(repo, gateway);
    var second = await Apply(repo, gateway);

    first.Attached.Should().Be(1);
    second.Attached.Should().Be(0, "the refund already has evidence");
    second.Eligible.Should().Be(0, "and the matcher itself now reports it as attached");
    repo.Fragments.Should().HaveCount(1);
  }

  // A request id collision is impossible by construction, but the fragment's
  // request id must not look like a zinc-issued one either: the card-refund
  // approve path claims "{withdrawalId}-{attempt}-{index}" fragments as its
  // own on a re-drive, and must not adopt a reconciled row.
  [Fact]
  public async Task An_attached_fragment_does_not_borrow_the_approve_paths_request_id_shape()
  {
    var repo = new FakeRepo();
    var withdrawal = repo.AddCandidate(amount: 100m);
    var gateway = new FakeGateway { Refunds = { Refund("rfd_1", 100m, From.AddDays(10)) } };

    await Apply(repo, gateway);

    var requestId = repo.Fragments.Single().RequestId;
    requestId.Should().Be($"{withdrawal.Id}-recon-rfd_1");
    requestId.Should().NotBe($"{withdrawal.Id}-1-0", "that shape belongs to the approve path");
  }

  // Airwallex forgets a refund 2 years after creation. Asking for a wider
  // window must clamp rather than silently report the unrecoverable slice as
  // "no refunds found".
  [Fact]
  public async Task The_window_is_clamped_to_the_gateways_retention_horizon()
  {
    var repo = new FakeRepo();
    var gateway = new FakeGateway();
    var runner = Runner(repo, gateway);

    var ancient = Now - RefundReconciler.RetentionWindow - TimeSpan.FromDays(365);
    var result = await runner.Report(ancient, Now, Now);

    result.IsSuccess().Should().BeTrue();
    var report = result.SuccessOrDefault();
    report.FromUtc.Should().Be(Now - RefundReconciler.RetentionWindow);
    gateway.Windows.Should().ContainSingle();
    gateway.Windows[0].From.Should().Be(Now - RefundReconciler.RetentionWindow);
  }

  // A window entirely beyond the horizon has nothing the gateway can answer,
  // so it must not even be asked.
  [Fact]
  public async Task A_window_entirely_beyond_the_horizon_costs_no_gateway_call()
  {
    var repo = new FakeRepo();
    var gateway = new FakeGateway();
    var runner = Runner(repo, gateway);

    var ancientEnd = Now - RefundReconciler.RetentionWindow - TimeSpan.FromDays(10);
    var result = await runner.Report(ancientEnd.AddDays(-30), ancientEnd, Now);

    result.IsSuccess().Should().BeTrue();
    result.SuccessOrDefault().Scanned.Should().Be(0);
    gateway.Windows.Should().BeEmpty();
  }

  [Fact]
  public async Task A_gateway_failure_fails_the_report_rather_than_reporting_an_empty_window()
  {
    var repo = new FakeRepo();
    var gateway = new FakeGateway { Failing = true };
    var runner = Runner(repo, gateway);

    var result = await runner.Report(From, To, Now);

    result.IsSuccess().Should().BeFalse("an empty report would read as 'no refunds exist'");
  }

  [Fact]
  public async Task An_empty_window_is_a_normal_empty_report()
  {
    var repo = new FakeRepo();
    var gateway = new FakeGateway();

    var report = await Report(repo, gateway);

    report.Scanned.Should().Be(0);
    report.Matched.Should().BeEmpty();
    report.Ambiguous.Should().BeEmpty();
  }

  // A failed refund returned no money, so its fragment must not claim the
  // amount — the refundable pool subtracts non-Failed fragments only.
  [Fact]
  public async Task A_failed_gateway_refund_is_attached_as_a_failed_fragment()
  {
    var repo = new FakeRepo();
    repo.AddCandidate(amount: 100m);
    var gateway = new FakeGateway
    {
      Refunds =
      {
        Refund("rfd_1", 100m, From.AddDays(10), outcome: PayoutOutcome.Failed),
      },
    };

    await Apply(repo, gateway);

    var fragment = repo.Fragments.Should().ContainSingle().Subject;
    fragment.Status.Should().Be(RefundFragmentStatus.Failed);
    fragment.SettledAt.Should().BeNull("nothing settled");
  }

  private static RefundReconciliationRunner Runner(FakeRepo repo, FakeGateway gateway) =>
    new(repo, gateway, NullLogger<RefundReconciliationRunner>.Instance);

  private static async Task<RefundReconciliationReport> Report(
    FakeRepo repo,
    FakeGateway gateway
  )
  {
    var result = await Runner(repo, gateway).Report(From, To, Now);
    result.IsSuccess().Should().BeTrue();
    return result.SuccessOrDefault();
  }

  private static async Task<RefundReconciliationApplyReport> Apply(
    FakeRepo repo,
    FakeGateway gateway
  )
  {
    var result = await Runner(repo, gateway).Apply(From, To, Now);
    result.IsSuccess().Should().BeTrue();
    return result.SuccessOrDefault();
  }

  private static GatewayRefund Refund(
    string id,
    decimal amount,
    DateTime createdAt,
    PayoutOutcome outcome = PayoutOutcome.Settled
  ) =>
    new()
    {
      Id = id,
      PaymentIntentId = Intent,
      Amount = amount,
      Outcome = outcome,
      AcquirerReferenceNumber = "12345678901234567890123",
      CreatedAt = createdAt,
      UpdatedAt = createdAt.AddHours(2),
      RequestId = null,
    };

  private sealed class FakeGateway : IRefundGateway
  {
    public List<GatewayRefund> Refunds { get; } = [];

    public bool Failing { get; init; }

    public List<(DateTime From, DateTime To)> Windows { get; } = [];

    public Task<Result<List<GatewayRefund>>> ListRefunds(DateTime fromUtc, DateTime toUtc)
    {
      Windows.Add((fromUtc, toUtc));
      if (Failing)
        return Task.FromResult<Result<List<GatewayRefund>>>(
          new HttpRequestException("gateway timeout")
        );
      return Task.FromResult<Result<List<GatewayRefund>>>(
        Refunds.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt < toUtc).ToList()
      );
    }

    public Task<Result<RefundConfirmation>> CreateRefund(RefundRequest request) =>
      throw new NotImplementedException();

    public Task<Result<RefundStatus>> GetRefundStatus(string refundId) =>
      throw new NotImplementedException();
  }

  private sealed class FakeRepo : IWithdrawalRefundRepository
  {
    public List<WithdrawalRefundFragment> Fragments { get; } = [];

    private readonly List<WithdrawalCandidate> candidates = [];

    public WithdrawalCandidate AddCandidate(decimal amount, DateTime? completedAt = null)
    {
      var candidate = new WithdrawalCandidate
      {
        Id = Guid.NewGuid(),
        WalletId = Wallet,
        UserId = User,
        Method = WithdrawalMethod.PayNow,
        Status = WithdrawStatus.Completed,
        Amount = amount,
        Fee = null,
        CreatedAt = From.AddDays(1),
        CompletedAt = completedAt ?? From.AddDays(10),
        AttachedRefundTotal = 0m,
      };
      this.candidates.Add(candidate);
      return candidate;
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
    ) =>
      Task.FromResult<Result<List<PaymentIntentOwner>>>(
        paymentIntentIds
          .Where(id => id == Intent)
          .Select(id => new PaymentIntentOwner
          {
            PaymentId = PaymentId,
            PaymentIntentId = id,
            WalletId = Wallet,
            UserId = User,
          })
          .ToList()
      );

    // Mirrors the real query: the attached total is recomputed from the
    // fragments that exist now, so a second run sees what the first wrote.
    public Task<Result<List<WithdrawalCandidate>>> ListCandidatesByWallets(
      IEnumerable<Guid> walletIds
    )
    {
      var ids = walletIds.ToHashSet();
      return Task.FromResult<Result<List<WithdrawalCandidate>>>(
        this.candidates.Where(c => ids.Contains(c.WalletId))
          .Select(c => c with
          {
            AttachedRefundTotal = Fragments
              .Where(f => f.WithdrawalId == c.Id && f.Status != RefundFragmentStatus.Failed)
              .Sum(f => f.Amount),
          })
          .ToList()
      );
    }

    public Task<Result<List<WithdrawalRefundFragment>>> CreateMany(
      IEnumerable<WithdrawalRefundFragment> fragments
    )
    {
      var list = fragments.ToList();
      Fragments.AddRange(list);
      return Task.FromResult<Result<List<WithdrawalRefundFragment>>>(list);
    }

    public Task<Result<List<FundingPayment>>> ListFundingPayments(Guid walletId, DateTime since) =>
      throw new NotImplementedException();

    public Task<Result<Dictionary<Guid, decimal>>> SumActiveRefundsByPayment(
      IEnumerable<Guid> paymentIds
    ) => throw new NotImplementedException();

    public Task<Result<List<WithdrawalRefundFragment>>> ListByWithdrawal(Guid withdrawalId) =>
      throw new NotImplementedException();

    public Task<Result<WithdrawalRefundFragment?>> GetByRequestId(string requestId) =>
      throw new NotImplementedException();

    public Task<Result<List<WithdrawalRefundFragment>>> ListSettledMissingArn(
      DateTime createdOnOrAfter,
      IEnumerable<Guid> excludeIds,
      int max
    ) => throw new NotImplementedException();

    public Task<Result<int>> CountUnbackfillableArn(DateTime createdBefore) =>
      throw new NotImplementedException();

    public Task<Result<WithdrawalRefundFragment?>> Update(
      Guid id,
      RefundFragmentStatus? status,
      string? airwallexRefundId,
      DateTime? settledAt,
      string? acquirerReferenceNumber
    ) => throw new NotImplementedException();
  }
}
