using CSharp_Result;
using Domain.Payment;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Payments;

// The gateway-fee capture pipeline: upsert planning is idempotent by
// FinancialTransactionId (replaying the same gateway response refreshes, it
// never duplicates), and the sync driver treats empty gateway answers as
// "not yet available" (stays missing, retried later) while bounding the
// per-call work.
public class GatewayFeeSyncTests
{
  private static GatewayFeeRecord Record(string ftId, string sourceId = "int_1", decimal fee = 1m) =>
    new()
    {
      SourceId = sourceId,
      SourceType = GatewayFeeSourceType.Payment,
      FinancialTransactionId = ftId,
      Amount = 100m,
      Fee = fee,
      Net = 100m - fee,
      Currency = "SGD",
      TransactedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
    };

  // ---- GatewayFeePlanner (pure upsert planning) ----

  [Fact]
  public void Unseen_ids_become_inserts()
  {
    var (insert, update) = GatewayFeePlanner.Plan([], [Record("ft_1"), Record("ft_2")]);

    insert.Should().HaveCount(2);
    update.Should().BeEmpty();
  }

  [Fact]
  public void Known_ids_become_updates_never_duplicates()
  {
    // the same FinancialTransactionId synced twice = refresh, not a new row
    var (insert, update) = GatewayFeePlanner.Plan(
      ["ft_1"],
      [Record("ft_1", fee: 2m), Record("ft_2")]
    );

    insert.Should().ContainSingle(x => x.FinancialTransactionId == "ft_2");
    update.Should().ContainSingle(x => x.FinancialTransactionId == "ft_1");
    update[0].Fee.Should().Be(2m);
  }

  [Fact]
  public void Duplicate_ids_within_one_batch_collapse_to_the_last()
  {
    var (insert, update) = GatewayFeePlanner.Plan(
      [],
      [Record("ft_1", fee: 1m), Record("ft_1", fee: 3m)]
    );

    insert.Should().ContainSingle();
    insert[0].Fee.Should().Be(3m);
    update.Should().BeEmpty();
  }

  [Fact]
  public void Replaying_an_already_synced_batch_is_all_updates()
  {
    var batch = new[] { Record("ft_1"), Record("ft_2") };

    var (insert, update) = GatewayFeePlanner.Plan(["ft_1", "ft_2"], batch);

    insert.Should().BeEmpty();
    update.Should().HaveCount(2);
  }

  // ---- GatewayFeeSyncService (driver) ----

  private sealed class FakeRepo : IGatewayFeeRepository
  {
    public List<PendingFeeSource> Pending { get; init; } = [];
    public List<GatewayFeeRecord> Upserted { get; } = [];
    public int UpsertCalls { get; private set; }

    public Task<Result<IEnumerable<PendingFeeSource>>> ListPendingSources(
      DateTime after,
      DateTime before,
      int max
    ) =>
      Task.FromResult(
        this.Pending.Take(max).ToArray().AsEnumerable().ToResult()
      );

    public Task<Result<int>> Upsert(IEnumerable<GatewayFeeRecord> records)
    {
      this.UpsertCalls++;
      var r = records.ToArray();
      this.Upserted.AddRange(r);
      return Task.FromResult((Result<int>)r.Length);
    }
  }

  private sealed class FakeGateway(Func<string, Result<IEnumerable<GatewayFeeLine>>> answer)
    : IGatewayFeeSource
  {
    public Task<Result<IEnumerable<GatewayFeeLine>>> BySource(PendingFeeSource source) =>
      Task.FromResult(answer(source.SourceId));
  }

  private static PendingFeeSource Source(string id, GatewayFeeSourceType type) =>
    new() { SourceId = id, SourceType = type };

  private static GatewayFeeLine Line(string ftId, decimal fee = 0.5m) =>
    new()
    {
      FinancialTransactionId = ftId,
      Amount = 10m,
      Fee = fee,
      Net = 10m - fee,
      Currency = "SGD",
      TransactedAt = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
    };

  private static GatewayFeeSyncService Service(FakeRepo repo, FakeGateway gateway) =>
    new(repo, gateway, NullLogger<GatewayFeeSyncService>.Instance);

  [Fact]
  public async Task Sources_with_fee_lines_are_synced_and_tagged_with_their_source()
  {
    var repo = new FakeRepo
    {
      Pending = [Source("int_1", GatewayFeeSourceType.Payment)],
    };
    var gateway = new FakeGateway(_ =>
      new[] { Line("ft_1") }.AsEnumerable().ToResult()
    );

    var r = await Service(repo, gateway).Sync(new GatewayFeeSyncQuery());

    r.SuccessOrDefault().Synced.Should().Be(1);
    r.SuccessOrDefault().Missing.Should().BeEmpty();
    r.SuccessOrDefault().HasMore.Should().BeFalse();
    repo.Upserted.Should().ContainSingle();
    repo.Upserted[0].SourceId.Should().Be("int_1");
    repo.Upserted[0].SourceType.Should().Be(GatewayFeeSourceType.Payment);
    repo.Upserted[0].FinancialTransactionId.Should().Be("ft_1");
  }

  [Fact]
  public async Task Empty_gateway_answer_stays_missing_and_writes_nothing()
  {
    // gateway fees post with delay: no rows yet is a normal answer — the
    // source is reported missing and retried on a later sync
    var repo = new FakeRepo
    {
      Pending = [Source("int_1", GatewayFeeSourceType.Payment)],
    };
    var gateway = new FakeGateway(_ =>
      Array.Empty<GatewayFeeLine>().AsEnumerable().ToResult()
    );

    var r = await Service(repo, gateway).Sync(new GatewayFeeSyncQuery());

    r.SuccessOrDefault().Synced.Should().Be(0);
    r.SuccessOrDefault().Missing.Should().Equal("int_1");
    repo.UpsertCalls.Should().Be(0);
  }

  [Fact]
  public async Task A_failing_source_stays_missing_without_failing_the_batch()
  {
    var repo = new FakeRepo
    {
      Pending =
      [
        Source("bad", GatewayFeeSourceType.Transfer),
        Source("good", GatewayFeeSourceType.Payment),
      ],
    };
    var gateway = new FakeGateway(id =>
      id == "bad"
        ? new HttpRequestException("boom")
        : new[] { Line("ft_ok") }.AsEnumerable().ToResult()
    );

    var r = await Service(repo, gateway).Sync(new GatewayFeeSyncQuery());

    r.SuccessOrDefault().Synced.Should().Be(1);
    r.SuccessOrDefault().Missing.Should().Equal("bad");
  }

  [Fact]
  public async Task Work_is_bounded_and_overflow_reports_has_more()
  {
    var repo = new FakeRepo
    {
      Pending = Enumerable
        .Range(0, GatewayFeeSyncService.MaxSourcesPerSync + 5)
        .Select(i => Source($"int_{i}", GatewayFeeSourceType.Payment))
        .ToList(),
    };
    var gateway = new FakeGateway(id =>
      new[] { Line($"ft_{id}") }.AsEnumerable().ToResult()
    );

    var r = await Service(repo, gateway).Sync(new GatewayFeeSyncQuery());

    r.SuccessOrDefault().Synced.Should().Be(GatewayFeeSyncService.MaxSourcesPerSync);
    r.SuccessOrDefault().HasMore.Should().BeTrue();
  }
}
