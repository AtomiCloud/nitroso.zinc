using CSharp_Result;
using Domain.Payment;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Payments;

// The KTMB card top-up sweep. Two things carry real money risk here and are
// pinned accordingly:
//
//   1. the four-part filter, because the issuing ledger carries every card
//      charge the company makes and anything that leaks through changes the
//      invoice's FX rate, and
//   2. the absolute value, because the gateway reports card spending as
//      negative and a signed total would invert the rate.
public class KtmbTopupSweepTests
{
  private static KtmbTopupLine Line(
    string id,
    decimal myr = -1000m,
    decimal sgd = -319.77m,
    string merchant = "KTMB GO TICKETING"
  ) =>
    new()
    {
      IssuingTransactionId = id,
      PostedAt = new DateTime(2026, 6, 14, 3, 0, 0, DateTimeKind.Utc),
      AmountMyr = myr,
      AmountSgd = sgd,
      MerchantName = merchant,
    };

  private static KtmbTopupRecord Record(string id, KtmbTopupSource source = KtmbTopupSource.Gateway) =>
    new()
    {
      IssuingTransactionId = id,
      PostedAt = new DateTime(2026, 6, 14, 3, 0, 0, DateTimeKind.Utc),
      AmountMyr = 1000m,
      AmountSgd = 319.77m,
      MerchantName = "KTMB GO TICKETING",
      Source = source,
    };

  // ---- KtmbTopupSweepPlanner.IsKtmbTopup (the filter) ----

  [Fact]
  public void A_cleared_approved_myr_ktmb_charge_is_a_topup()
  {
    KtmbTopupSweepPlanner.IsKtmbTopup("MYR", "APPROVED", "CLEARING", "KTMB GO TICKETING")
      .Should()
      .BeTrue();
  }

  [Theory]
  // an SGD or USD charge is some other vendor entirely
  [InlineData("SGD", "APPROVED", "CLEARING", "KTMB GO TICKETING")]
  // a failed attempt cost us nothing and must not enter the FX blend
  [InlineData("MYR", "FAILED", "CLEARING", "KTMB GO TICKETING")]
  // the AUTHORIZATION hold is superseded by its CLEARING; counting both
  // would double every top-up
  [InlineData("MYR", "APPROVED", "AUTHORIZATION", "KTMB GO TICKETING")]
  // another merchant's MYR charge is not a ticket purchase
  [InlineData("MYR", "APPROVED", "CLEARING", "CLOUDFLARE")]
  public void Everything_else_on_the_card_is_excluded(
    string currency,
    string status,
    string type,
    string merchant
  )
  {
    KtmbTopupSweepPlanner.IsKtmbTopup(currency, status, type, merchant).Should().BeFalse();
  }

  [Fact]
  public void A_missing_merchant_is_not_a_topup()
  {
    KtmbTopupSweepPlanner.IsKtmbTopup("MYR", "APPROVED", "CLEARING", null).Should().BeFalse();
  }

  [Fact]
  public void The_merchant_match_is_loose_and_case_insensitive()
  {
    // the merchant string is gateway-controlled and has changed before
    KtmbTopupSweepPlanner.IsKtmbTopup("myr", "approved", "clearing", "ktmb online sdn bhd")
      .Should()
      .BeTrue();
  }

  // ---- KtmbTopupSweepPlanner (window + mapping) ----

  [Fact]
  public void First_run_sweeps_full_history()
  {
    KtmbTopupSweepPlanner.WindowStart(null)
      .Should()
      .Be(KtmbTopupSweepPlanner.FullHistoryStartUtc);
  }

  [Fact]
  public void Later_runs_sweep_from_the_watermark_minus_the_overlap()
  {
    var watermark = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

    KtmbTopupSweepPlanner.WindowStart(watermark)
      .Should()
      .Be(watermark - KtmbTopupSweepPlanner.Overlap);
  }

  [Fact]
  public void Both_amounts_are_stored_positive()
  {
    // the gateway reports card spending as negative (money leaving); a signed
    // total would invert the MYR/SGD rate the invoice divides
    var record = KtmbTopupSweepPlanner.ToRecord(Line("itx_1", myr: -1000m, sgd: -319.77m));

    record.AmountMyr.Should().Be(1000m);
    record.AmountSgd.Should().Be(319.77m);
    record.Source.Should().Be(KtmbTopupSource.Gateway);
    record.MerchantName.Should().Be("KTMB GO TICKETING", "kept verbatim for audit");
  }

  // ---- KtmbTopupPlanner (upsert planning) ----

  [Fact]
  public void Unseen_ids_insert_and_known_ids_refresh()
  {
    var (insert, update) = KtmbTopupPlanner.Plan(
      ["itx_1"],
      [Record("itx_1"), Record("itx_2")]
    );

    insert.Select(x => x.IssuingTransactionId).Should().Equal("itx_2");
    update.Select(x => x.IssuingTransactionId).Should().Equal("itx_1");
  }

  [Fact]
  public void Replaying_the_same_gateway_response_writes_no_duplicates()
  {
    // the sweep's overlap window re-reads the same rows every tick
    var (insert, update) = KtmbTopupPlanner.Plan(["itx_1", "itx_2"], [Record("itx_1"), Record("itx_2")]);

    insert.Should().BeEmpty();
    update.Should().HaveCount(2);
  }

  [Fact]
  public void Duplicate_ids_inside_one_batch_collapse_to_the_last()
  {
    var first = Record("itx_1") with { AmountMyr = 1m };
    var last = Record("itx_1") with { AmountMyr = 2m };

    var (insert, _) = KtmbTopupPlanner.Plan([], [first, last]);

    insert.Should().ContainSingle();
    insert[0].AmountMyr.Should().Be(2m);
  }

  // ---- KtmbTopupSweep (driver) ----

  private sealed class FakeRepo : IKtmbTopupRepository
  {
    public DateTime? Watermark { get; init; }
    public List<KtmbTopupRecord> Upserted { get; } = [];

    public Task<Result<int>> Upsert(IEnumerable<KtmbTopupRecord> records)
    {
      var r = records.ToArray();
      this.Upserted.AddRange(r);
      return Task.FromResult((Result<int>)r.Length);
    }

    public Task<Result<DateTime?>> LatestGatewayPostedAt() =>
      Task.FromResult((Result<DateTime?>)this.Watermark);
  }

  private sealed class FakeGateway(
    Func<DateTime, DateTime, Result<IEnumerable<KtmbTopupLine>>> answer
  ) : IKtmbTopupSource
  {
    public List<(DateTime From, DateTime To)> Calls { get; } = [];

    public Task<Result<IEnumerable<KtmbTopupLine>>> InRange(DateTime fromUtc, DateTime toUtc)
    {
      this.Calls.Add((fromUtc, toUtc));
      return Task.FromResult(answer(fromUtc, toUtc));
    }
  }

  private static KtmbTopupSweep Sweep(FakeRepo repo, FakeGateway gateway) =>
    new(repo, gateway, NullLogger<KtmbTopupSweep>.Instance);

  [Fact]
  public async Task First_sweep_covers_full_history_and_writes_every_line()
  {
    var repo = new FakeRepo();
    var gateway = new FakeGateway((_, _) =>
      new[] { Line("itx_1"), Line("itx_2", myr: -5000m, sgd: -1598.85m) }.AsEnumerable().ToResult()
    );
    var now = new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc);

    var r = await Sweep(repo, gateway).Sweep(now);

    r.IsSuccess().Should().BeTrue();
    var report = r.SuccessOrDefault();
    report.FromUtc.Should().Be(KtmbTopupSweepPlanner.FullHistoryStartUtc);
    report.ToUtc.Should().Be(now);
    report.Wrote.Should().Be(2);
    repo.Upserted.Should().OnlyContain(x => x.Source == KtmbTopupSource.Gateway);
    repo.Upserted.Sum(x => x.AmountMyr).Should().Be(6000m, "stored positive");
  }

  [Fact]
  public async Task Incremental_sweep_asks_from_the_watermark_minus_the_overlap()
  {
    var watermark = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
    var repo = new FakeRepo { Watermark = watermark };
    var gateway = new FakeGateway((_, _) =>
      Array.Empty<KtmbTopupLine>().AsEnumerable().ToResult()
    );
    var now = new DateTime(2026, 8, 15, 1, 0, 0, DateTimeKind.Utc);

    var r = await Sweep(repo, gateway).Sweep(now);

    r.IsSuccess().Should().BeTrue();
    gateway.Calls.Should().ContainSingle();
    gateway.Calls[0].From.Should().Be(watermark - KtmbTopupSweepPlanner.Overlap);
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
