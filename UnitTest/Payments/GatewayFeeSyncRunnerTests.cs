using CSharp_Result;
using Domain.Payment;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Payments;

// The recurring-sync drain loop: keep calling Sync with an unbounded range
// while more pending sources exist, but never spin forever — the run is
// bounded by maxBatches, sources found missing are excluded from the run's
// later batches (so a missing head can't starve the tail), and cancellation
// is honoured between batches.
public class GatewayFeeSyncRunnerTests
{
  private sealed class FakeSyncService(Func<int, Result<GatewayFeeSyncResult>> answer)
    : IGatewayFeeSyncService
  {
    public List<GatewayFeeSyncQuery> Queries { get; } = [];

    public Task<Result<GatewayFeeSyncResult>> Sync(GatewayFeeSyncQuery query)
    {
      this.Queries.Add(query);
      return Task.FromResult(answer(this.Queries.Count - 1));
    }
  }

  private static GatewayFeeSyncResult Batch(int synced, bool hasMore, params string[] missing) =>
    new()
    {
      Synced = synced,
      Missing = missing,
      HasMore = hasMore,
    };

  private static GatewayFeeSyncRunner Runner(FakeSyncService service) =>
    new(service, NullLogger<GatewayFeeSyncRunner>.Instance);

  [Fact]
  public async Task Drains_batches_until_nothing_is_pending()
  {
    var service = new FakeSyncService(call =>
      call switch
      {
        0 => Batch(200, hasMore: true),
        1 => Batch(200, hasMore: true),
        _ => Batch(37, hasMore: false, "int_late"),
      }
    );

    var r = await Runner(service).Drain(GatewayFeeSyncRunner.MaxBatchesPerRun);

    var report = r.SuccessOrDefault();
    report.Batches.Should().Be(3);
    report.Synced.Should().Be(437);
    report.Missing.Should().Be(1);
    service.Queries.Should().HaveCount(3);
  }

  [Fact]
  public async Task Every_batch_uses_an_unbounded_range()
  {
    var service = new FakeSyncService(call => Batch(1, hasMore: call < 2));

    await Runner(service).Drain(GatewayFeeSyncRunner.MaxBatchesPerRun);

    service.Queries.Should().HaveCount(3);
    service.Queries.Should().OnlyContain(q => q.After == null && q.Before == null);
  }

  [Fact]
  public async Task One_run_never_exceeds_the_batch_bound()
  {
    var service = new FakeSyncService(_ => Batch(200, hasMore: true));

    var r = await Runner(service).Drain(5);

    r.SuccessOrDefault().Batches.Should().Be(5);
    service.Queries.Should().HaveCount(5);
  }

  [Fact]
  public async Task Missing_sources_are_excluded_from_later_batches()
  {
    // the worklist is ordered, so a head of still-missing sources would be
    // re-listed by every batch and starve the tail — each batch must skip
    // everything the run already found missing (the next run retries them)
    var service = new FakeSyncService(call =>
      call == 0 ? Batch(0, hasMore: true, "int_1", "int_2") : Batch(5, hasMore: false, "int_3")
    );

    var r = await Runner(service).Drain(GatewayFeeSyncRunner.MaxBatchesPerRun);

    service.Queries[0].ExcludeSourceIds.Should().BeEmpty();
    service.Queries[1].ExcludeSourceIds.Should().BeEquivalentTo("int_1", "int_2");
    var report = r.SuccessOrDefault();
    report.Batches.Should().Be(2);
    report.Synced.Should().Be(5);
    report.Missing.Should().Be(3);
  }

  [Fact]
  public async Task Cancellation_stops_the_run_between_batches()
  {
    var service = new FakeSyncService(_ => Batch(200, hasMore: true));
    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();

    var r = await Runner(service).Drain(GatewayFeeSyncRunner.MaxBatchesPerRun, cts.Token);

    r.SuccessOrDefault().Batches.Should().Be(0);
    service.Queries.Should().BeEmpty();
  }

  [Fact]
  public async Task A_failed_sync_surfaces_as_a_failure()
  {
    var service = new FakeSyncService(_ => new HttpRequestException("gateway down"));

    var r = await Runner(service).Drain(GatewayFeeSyncRunner.MaxBatchesPerRun);

    r.IsFailure().Should().BeTrue();
  }
}
