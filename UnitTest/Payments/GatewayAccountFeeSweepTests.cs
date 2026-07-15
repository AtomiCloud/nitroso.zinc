using CSharp_Result;
using Domain.Payment;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Payments;

// The account-level fee sweep: full history on the first run (no watermark),
// incremental from the watermark minus the overlap window after, and each
// FEE-type line stored as SourceType = AccountFee with Fee = |amount| while
// Amount/Net stay as the gateway reported them.
public class GatewayAccountFeeSweepTests
{
  private static GatewayAccountFeeLine Line(
    string ftId,
    decimal amount = -61m,
    string sourceId = "fbl_1"
  ) =>
    new()
    {
      SourceId = sourceId,
      FinancialTransactionId = ftId,
      Amount = amount,
      Net = amount,
      Currency = "SGD",
      TransactedAt = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc),
    };

  // ---- GatewayAccountFeePlanner (pure window + mapping rules) ----

  [Fact]
  public void First_run_sweeps_full_history()
  {
    GatewayAccountFeePlanner.WindowStart(null)
      .Should()
      .Be(GatewayAccountFeePlanner.FullHistoryStartUtc);
  }

  [Fact]
  public void Later_runs_sweep_from_the_watermark_minus_the_overlap()
  {
    var watermark = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

    GatewayAccountFeePlanner.WindowStart(watermark)
      .Should()
      .Be(watermark - GatewayAccountFeePlanner.Overlap);
  }

  [Fact]
  public void Lines_store_as_account_fees_with_the_absolute_amount_as_the_fee()
  {
    var record = GatewayAccountFeePlanner.ToRecord(Line("ft_1", amount: -61m));

    record.SourceType.Should().Be(GatewayFeeSourceType.AccountFee);
    record.SourceId.Should().Be("fbl_1");
    record.FinancialTransactionId.Should().Be("ft_1");
    record.Fee.Should().Be(61m, "the fee take is stored positive");
    record.Amount.Should().Be(-61m, "amount stays as the gateway reported it");
    record.Net.Should().Be(-61m, "net stays as the gateway reported it");
  }

  // ---- GatewayAccountFeeSweep (driver) ----

  private sealed class FakeRepo : IGatewayFeeRepository
  {
    public DateTime? Watermark { get; init; }
    public List<GatewayFeeRecord> Upserted { get; } = [];

    public Task<Result<IEnumerable<PendingFeeSource>>> ListPendingSources(
      DateTime after,
      DateTime before,
      IReadOnlyCollection<string> exclude,
      int max
    ) => Task.FromResult(Array.Empty<PendingFeeSource>().AsEnumerable().ToResult());

    public Task<Result<int>> Upsert(IEnumerable<GatewayFeeRecord> records)
    {
      var r = records.ToArray();
      this.Upserted.AddRange(r);
      return Task.FromResult((Result<int>)r.Length);
    }

    public Task<Result<DateTime?>> LatestAccountFeeTransactedAt() =>
      Task.FromResult((Result<DateTime?>)this.Watermark);
  }

  private sealed class FakeGateway(
    Func<DateTime, DateTime, Result<IEnumerable<GatewayAccountFeeLine>>> answer
  ) : IGatewayAccountFeeSource
  {
    public List<(DateTime From, DateTime To)> Calls { get; } = [];

    public Task<Result<IEnumerable<GatewayAccountFeeLine>>> InRange(
      DateTime fromUtc,
      DateTime toUtc
    )
    {
      this.Calls.Add((fromUtc, toUtc));
      return Task.FromResult(answer(fromUtc, toUtc));
    }
  }

  private static GatewayAccountFeeSweep Sweep(FakeRepo repo, FakeGateway gateway) =>
    new(repo, gateway, NullLogger<GatewayAccountFeeSweep>.Instance);

  [Fact]
  public async Task First_sweep_covers_full_history_and_writes_every_line()
  {
    var repo = new FakeRepo();
    var gateway = new FakeGateway((_, _) =>
      new[] { Line("ft_1"), Line("ft_2", amount: -0.3m, sourceId: "fbl_2") }
        .AsEnumerable()
        .ToResult()
    );
    var now = new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc);

    var r = await Sweep(repo, gateway).Sweep(now);

    r.IsSuccess().Should().BeTrue();
    var report = r.SuccessOrDefault();
    report.FromUtc.Should().Be(GatewayAccountFeePlanner.FullHistoryStartUtc);
    report.ToUtc.Should().Be(now);
    report.Wrote.Should().Be(2);
    repo.Upserted.Should().HaveCount(2);
    repo.Upserted.Should().OnlyContain(x => x.SourceType == GatewayFeeSourceType.AccountFee);
  }

  [Fact]
  public async Task Incremental_sweep_asks_from_the_watermark_minus_the_overlap()
  {
    var watermark = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
    var repo = new FakeRepo { Watermark = watermark };
    var gateway = new FakeGateway((_, _) =>
      Array.Empty<GatewayAccountFeeLine>().AsEnumerable().ToResult()
    );
    var now = new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc);

    var r = await Sweep(repo, gateway).Sweep(now);

    r.IsSuccess().Should().BeTrue();
    gateway.Calls.Should().ContainSingle();
    gateway.Calls[0].From.Should().Be(watermark - GatewayAccountFeePlanner.Overlap);
    gateway.Calls[0].To.Should().Be(now);
    r.SuccessOrDefault().Wrote.Should().Be(0, "an empty window is a normal answer");
  }

  [Fact]
  public async Task A_failed_gateway_listing_fails_the_sweep_without_writing()
  {
    var repo = new FakeRepo();
    var gateway = new FakeGateway((_, _) => new HttpRequestException("boom"));

    var r = await Sweep(repo, gateway)
      .Sweep(new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc));

    r.IsSuccess().Should().BeFalse();
    repo.Upserted.Should().BeEmpty();
  }
}
