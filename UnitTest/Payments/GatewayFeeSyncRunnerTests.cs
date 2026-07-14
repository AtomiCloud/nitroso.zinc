using CSharp_Result;
using Domain.Payment;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Payments;

// The recurring-sync drain loop: keep calling Sync with an unbounded range
// while more pending sources exist, but never spin forever — stop at the
// batch bound and stop early when a batch makes no progress (everything left
// is missing at the gateway; the next run retries it).
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
    var service = new FakeSyncService(_ => Batch(1, hasMore: false));

    await Runner(service).Drain(GatewayFeeSyncRunner.MaxBatchesPerRun);

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
  public async Task A_batch_with_no_progress_ends_the_run_early()
  {
    // hasMore with zero synced means everything reachable is still missing
    // at the gateway — retrying within the same run would hammer the same
    // sources, so the run stops and the next tick retries
    var service = new FakeSyncService(_ => Batch(0, hasMore: true, "int_1", "int_2"));

    var r = await Runner(service).Drain(GatewayFeeSyncRunner.MaxBatchesPerRun);

    var report = r.SuccessOrDefault();
    report.Batches.Should().Be(1);
    report.Synced.Should().Be(0);
    report.Missing.Should().Be(2);
  }

  [Fact]
  public async Task A_failed_sync_surfaces_as_a_failure()
  {
    var service = new FakeSyncService(_ => new HttpRequestException("gateway down"));

    var r = await Runner(service).Drain(GatewayFeeSyncRunner.MaxBatchesPerRun);

    r.IsFailure().Should().BeTrue();
  }
}
