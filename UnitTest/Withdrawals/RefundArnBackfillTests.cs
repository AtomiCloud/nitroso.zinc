using CSharp_Result;
using Domain.Withdrawal;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Withdrawals;

public class RefundArnBackfillTests
{
  private static readonly DateTime Now = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

  [Fact]
  public async Task Backfill_captures_the_arn_of_a_settled_fragment_that_has_none()
  {
    var repo = new FakeRepo();
    var fragment = repo.AddSettled("rf_1", Now.AddDays(-30));
    var gateway = new FakeGateway { Arns = { ["rf_1"] = "12345678901234567890123" } };

    var report = await Drain(repo, gateway);

    report.Captured.Should().Be(1);
    report.Pending.Should().Be(0);
    repo.Get(fragment.Id).AcquirerReferenceNumber.Should().Be("12345678901234567890123");
  }

  // THE trap this backfill exists to avoid: Airwallex forgets a refund 2
  // years after creation, so those rows can never gain an ARN. A naive
  // "still missing an ARN" backlog would re-query them on every tick until
  // the end of time. They must fall out of the backlog and be reported.
  [Fact]
  public async Task Backlog_excludes_fragments_beyond_the_retention_horizon_and_reports_them()
  {
    var repo = new FakeRepo();
    var recent = repo.AddSettled("rf_recent", Now.AddDays(-10));
    var ancient = repo.AddSettled(
      "rf_ancient",
      Now - RefundArnBackfillRunner.RetentionWindow - TimeSpan.FromDays(1)
    );
    var gateway = new FakeGateway
    {
      Arns = { ["rf_recent"] = "arn-recent", ["rf_ancient"] = "arn-ancient" },
    };

    var report = await Drain(repo, gateway);

    report.Unbackfillable.Should().Be(1, "the ancient fragment is past the retention horizon");
    gateway.Looked.Should()
      .Equal(["rf_recent"], "an unbackfillable row must not cost a gateway call");
    repo.Get(recent.Id).AcquirerReferenceNumber.Should().Be("arn-recent");
    repo.Get(ancient.Id)
      .AcquirerReferenceNumber.Should()
      .BeNull("the gateway can no longer answer for it");
  }

  // Partial-update semantics: a null from the gateway means "the network has
  // not published one yet", never "erase what we stored".
  [Fact]
  public async Task A_null_arn_from_the_gateway_never_clobbers_a_stored_value()
  {
    var repo = new FakeRepo();
    var fragment = repo.AddSettled("rf_1", Now.AddDays(-30));
    await repo.Update(fragment.Id, null, null, null, "already-captured");

    // gateway knows the refund but reports no ARN
    var gateway = new FakeGateway();
    await Drain(repo, gateway);

    repo.Get(fragment.Id).AcquirerReferenceNumber.Should().Be("already-captured");
    // and it is no longer in the backlog at all, so it costs nothing
    gateway.Looked.Should().BeEmpty();
  }

  [Fact]
  public async Task A_settled_fragment_the_gateway_has_no_arn_for_stays_pending_for_the_next_tick()
  {
    var repo = new FakeRepo();
    var fragment = repo.AddSettled("rf_1", Now.AddDays(-30));
    var gateway = new FakeGateway();

    var report = await Drain(repo, gateway);

    report.Scanned.Should().Be(1);
    report.Captured.Should().Be(0);
    report.Pending.Should().Be(1);
    repo.Get(fragment.Id).AcquirerReferenceNumber.Should().BeNull();
    // one lookup, not a spin: the row is excluded from the rest of this drain
    gateway.Looked.Should().Equal("rf_1");
  }

  [Fact]
  public async Task Only_settled_fragments_with_a_gateway_refund_id_are_in_the_backlog()
  {
    var repo = new FakeRepo();
    repo.AddSettled("rf_settled", Now.AddDays(-1));
    repo.Add("rf_created", Now.AddDays(-1), RefundFragmentStatus.Created);
    repo.Add("rf_failed", Now.AddDays(-1), RefundFragmentStatus.Failed);
    repo.Add(null, Now.AddDays(-1), RefundFragmentStatus.Settled);
    var gateway = new FakeGateway { Arns = { ["rf_settled"] = "arn" } };

    var report = await Drain(repo, gateway);

    report.Captured.Should().Be(1);
    gateway.Looked.Should()
      .Equal(["rf_settled"], "unsettled, failed and never-created refunds have no ARN to fetch");
  }

  [Fact]
  public async Task A_gateway_failure_is_skipped_and_left_for_the_next_tick()
  {
    var repo = new FakeRepo();
    var fragment = repo.AddSettled("rf_1", Now.AddDays(-30));
    var gateway = new FakeGateway { Failing = { "rf_1" } };

    var report = await Drain(repo, gateway);

    report.Captured.Should().Be(0);
    report.Pending.Should().Be(1);
    repo.Get(fragment.Id).AcquirerReferenceNumber.Should().BeNull();
  }

  private static async Task<RefundArnBackfillReport> Drain(FakeRepo repo, FakeGateway gateway)
  {
    var runner = new RefundArnBackfillRunner(
      repo,
      gateway,
      NullLogger<RefundArnBackfillRunner>.Instance
    );
    var result = await runner.Drain(Now, RefundArnBackfillRunner.MaxBatchesPerRun);
    result.IsSuccess().Should().BeTrue();
    return result.SuccessOrDefault();
  }

  private sealed class FakeGateway : IRefundGateway
  {
    // refund id -> ARN the gateway reports; absent = the gateway knows the
    // refund but has no ARN for it yet
    public Dictionary<string, string> Arns { get; } = [];

    public HashSet<string> Failing { get; } = [];

    public List<string> Looked { get; } = [];

    public Task<Result<RefundConfirmation>> CreateRefund(RefundRequest request) =>
      throw new NotImplementedException();

    public Task<Result<List<GatewayRefund>>> ListRefunds(DateTime fromUtc, DateTime toUtc) =>
      throw new NotImplementedException();

    public Task<Result<RefundStatus>> GetRefundStatus(string refundId)
    {
      Looked.Add(refundId);
      if (Failing.Contains(refundId))
        return Task.FromResult<Result<RefundStatus>>(new HttpRequestException("gateway timeout"));
      return Task.FromResult<Result<RefundStatus>>(
        new RefundStatus
        {
          Outcome = PayoutOutcome.Settled,
          ConfirmationNumber = refundId,
          AcquirerReferenceNumber = Arns.GetValueOrDefault(refundId),
        }
      );
    }
  }

  // Mirrors the real repository's backlog predicate; the point of the suite
  // is the runner's bounding and partial-update behaviour on top of it.
  private sealed class FakeRepo : IWithdrawalRefundRepository
  {
    private readonly List<WithdrawalRefundFragment> fragments = [];

    public WithdrawalRefundFragment AddSettled(string refundId, DateTime createdAt) =>
      Add(refundId, createdAt, RefundFragmentStatus.Settled);

    public WithdrawalRefundFragment Add(
      string? refundId,
      DateTime createdAt,
      RefundFragmentStatus status
    )
    {
      var fragment = new WithdrawalRefundFragment
      {
        Id = Guid.NewGuid(),
        WithdrawalId = Guid.NewGuid(),
        PaymentId = Guid.NewGuid(),
        PaymentIntentId = "pi_1",
        AirwallexRefundId = refundId,
        RequestId = $"req-{this.fragments.Count}",
        Amount = 10m,
        Status = status,
        CreatedAt = createdAt,
        SettledAt = status == RefundFragmentStatus.Settled ? createdAt : null,
      };
      this.fragments.Add(fragment);
      return fragment;
    }

    public WithdrawalRefundFragment Get(Guid id) => this.fragments.Single(f => f.Id == id);

    public Task<Result<List<WithdrawalRefundFragment>>> ListSettledMissingArn(
      DateTime createdOnOrAfter,
      IEnumerable<Guid> excludeIds,
      int max
    )
    {
      var excluded = excludeIds.ToHashSet();
      return Task.FromResult<Result<List<WithdrawalRefundFragment>>>(
        this.fragments.Where(f =>
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

    public Task<Result<int>> CountUnbackfillableArn(DateTime createdBefore) =>
      Task.FromResult<Result<int>>(
        this.fragments.Count(f =>
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
      var idx = this.fragments.FindIndex(f => f.Id == id);
      if (idx < 0)
        return Task.FromResult<Result<WithdrawalRefundFragment?>>((WithdrawalRefundFragment?)null);
      var updated = this.fragments[idx] with
      {
        Status = status ?? this.fragments[idx].Status,
        AirwallexRefundId = airwallexRefundId ?? this.fragments[idx].AirwallexRefundId,
        SettledAt = settledAt ?? this.fragments[idx].SettledAt,
        AcquirerReferenceNumber =
          acquirerReferenceNumber ?? this.fragments[idx].AcquirerReferenceNumber,
      };
      this.fragments[idx] = updated;
      return Task.FromResult<Result<WithdrawalRefundFragment?>>(updated);
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

    public Task<Result<List<WithdrawalRefundFragment>>> CreateMany(
      IEnumerable<WithdrawalRefundFragment> fragments
    ) => throw new NotImplementedException();

    public Task<Result<List<WithdrawalRefundFragment>>> ListByAirwallexRefundIds(
      IEnumerable<string> refundIds
    ) => throw new NotImplementedException();

    public Task<Result<List<PaymentIntentOwner>>> ListPaymentIntentOwners(
      IEnumerable<string> paymentIntentIds
    ) => throw new NotImplementedException();

    public Task<Result<List<WithdrawalCandidate>>> ListCandidatesByWallets(
      IEnumerable<Guid> walletIds
    ) => throw new NotImplementedException();
  }
}
