using Domain.Booking;
using Domain.Invoice;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Invoices;

// GET Invoice/inputs prices a CLOSED month at the fare tin actually paid KTMB
// (the average Bookings.KtmbAmount, MYR only) unless the owner has entered a
// KtmbCosts override for it. Measured production averages, Woodlands->JB:
// Jun 16.131, Jul 16.0377, Aug 16.1694, Sep 16.221; JB->Woodlands RM 5 flat.
public class InvoiceInputMeasuredFareTests
{
  private static readonly DateOnly September = new(2026, 9, 1);
  private static readonly DateOnly October = new(2026, 10, 1);

  // 10 Oct 2026 — September is closed, October is open
  private static readonly DateTime Now = new(2026, 10, 10, 4, 0, 0, DateTimeKind.Utc);

  private static DateTime Sgt(int y, int m, int d, int h = 0, int min = 0) =>
    DateTime.SpecifyKind(new DateTime(y, m, d, h, min, 0).AddHours(-8), DateTimeKind.Utc);

  private static InvoiceInputDailySum Day(
    DateOnly date,
    int direction,
    int completed,
    int priced,
    decimal pricedMyr
  ) =>
    new()
    {
      Date = date,
      Direction = direction,
      Deposits = 0m,
      PaymentMethodFees = 0m,
      GatewayFees = 0m,
      RefundFees = 0m,
      CompletedCount = completed,
      CompletedRevenue = 0m,
      PriorityPaidCount = 0,
      PriorityFee = 0m,
      PriorityFreeCount = 0,
      TerminatedCount = 0,
      TerminatedCollected = 0m,
      WithdrawalCount = 0,
      WithdrawalTotal = 0m,
      WithdrawalFeeIncome = 0m,
      WithdrawalWithFeeCount = 0,
      TopupMyr = 0m,
      TopupSgd = 0m,
      PricedCount = priced,
      PricedMyr = pricedMyr,
    };

  private static KtmbCostChange Override(TrainDirection direction, decimal cost, DateTime at) =>
    new()
    {
      Id = Guid.NewGuid(),
      Direction = direction,
      Cost = cost,
      EffectiveAt = at,
      CreatedAt = at,
    };

  private static InvoiceInputRoute Route(InvoiceInputRow row, InvoiceDirection dir) =>
    row.Routes.Single(r => r.Direction == dir);

  // ---- measured fare math ----

  [Fact]
  public void MeasuredFare_IsSumOverCount_AcrossTheMonthsDays()
  {
    // sum-over-count across days, NOT an average of daily averages: a
    // 1-ticket day must not weigh the same as a 100-ticket one
    var days = new[]
    {
      Day(new DateOnly(2026, 9, 1), 2, 100, 100, 1600m),
      Day(new DateOnly(2026, 9, 2), 2, 1, 1, 20m),
    };

    var r = Route(InvoiceInputCalculator.Gather(days, September, [], Now), InvoiceDirection.WoodlandsToJb);

    // 1620 / 101 = 16.0396..., not (16.00 + 20.00) / 2
    r.MeasuredFare.Should().Be(16.04m);
    r.PricedTickets.Should().Be(101);
    r.PricedCoverage.Should().Be(1m);
  }

  [Fact]
  public void MeasuredFare_IsRoundedToCents_AsTheDocumentPrintsIt()
  {
    // Sep W->JB measured 16.221 -> 16.22; Oct 16.2521 -> 16.25
    InvoiceInputCalculator.MeasuredFare(1000, 16221m).Should().Be(16.22m);
    InvoiceInputCalculator.MeasuredFare(10000, 162521m).Should().Be(16.25m);
    InvoiceInputCalculator.MeasuredFare(3, 50m).Should().Be(16.67m);
    // midpoint rounds away from zero, never banker's
    InvoiceInputCalculator.MeasuredFare(2, 32.25m).Should().Be(16.13m);
  }

  [Fact]
  public void NoPricedTickets_NoMeasuredFare()
  {
    InvoiceInputCalculator.MeasuredFare(0, 0m).Should().BeNull();
    InvoiceInputCalculator.Coverage(0, 0).Should().BeNull();
    InvoiceInputCalculator.Coverage(0, 10).Should().Be(0m);
  }

  [Fact]
  public void Coverage_IsPricedOverTickets()
  {
    InvoiceInputCalculator.Coverage(99, 100).Should().Be(0.99m);
    InvoiceInputCalculator.Coverage(2364, 2386).Should().BeApproximately(0.99078m, 0.00001m);
  }

  // ---- precedence ----

  [Fact]
  public void ClosedMonth_FullCoverage_UsesMeasured()
  {
    var days = new[]
    {
      Day(new DateOnly(2026, 9, 10), 1, 200, 200, 1000m),
      Day(new DateOnly(2026, 9, 10), 2, 100, 100, 1622.1m),
    };

    var row = InvoiceInputCalculator.Gather(days, September, [], Now);

    Route(row, InvoiceDirection.JbToWoodlands).KtmbFare.Should().Be(5m);
    Route(row, InvoiceDirection.JbToWoodlands).KtmbFareSource.Should().Be(InvoiceFareSource.Measured);
    Route(row, InvoiceDirection.WoodlandsToJb).KtmbFare.Should().Be(16.22m);
  }

  [Fact]
  public void Override_BeatsMeasured()
  {
    var days = new[] { Day(new DateOnly(2026, 9, 10), 2, 100, 100, 1622.1m) };
    var costs = new[] { Override(TrainDirection.WToJ, 16.50m, Sgt(2026, 9, 1)) };

    var r = Route(InvoiceInputCalculator.Gather(days, September, costs, Now), InvoiceDirection.WoodlandsToJb);

    r.KtmbFare.Should().Be(16.50m);
    r.KtmbFareSource.Should().Be(InvoiceFareSource.Override);
    // the measured figure is still reported for comparison
    r.MeasuredFare.Should().Be(16.22m);
  }

  [Fact]
  public void LowCoverage_BlocksWithNull()
  {
    // 98 of 100 priced: below 99%, the average is not trusted
    var days = new[] { Day(new DateOnly(2026, 9, 10), 2, 100, 98, 1589.658m) };

    var r = Route(InvoiceInputCalculator.Gather(days, September, [], Now), InvoiceDirection.WoodlandsToJb);

    r.KtmbFare.Should().BeNull();
    r.KtmbFareSource.Should().BeNull();
    r.MeasuredFare.Should().Be(16.22m);
    r.PricedCoverage.Should().Be(0.98m);
  }

  [Fact]
  public void CoverageExactlyAtThreshold_UsesMeasured()
  {
    var days = new[] { Day(new DateOnly(2026, 9, 10), 2, 100, 99, 1605.879m) };

    var r = Route(InvoiceInputCalculator.Gather(days, September, [], Now), InvoiceDirection.WoodlandsToJb);

    r.KtmbFareSource.Should().Be(InvoiceFareSource.Measured);
    r.KtmbFare.Should().Be(16.22m);
  }

  [Fact]
  public void OpenMonth_NeverAutoPrices()
  {
    var days = new[] { Day(new DateOnly(2026, 10, 5), 2, 100, 100, 1625.21m) };

    var r = Route(InvoiceInputCalculator.Gather(days, October, [], Now), InvoiceDirection.WoodlandsToJb);

    r.KtmbFare.Should().BeNull();
    r.KtmbFareSource.Should().BeNull();
    r.MeasuredFare.Should().Be(16.25m);
  }

  [Fact]
  public void OpenMonth_StillHonoursAnOverride()
  {
    var costs = new[] { Override(TrainDirection.WToJ, 16.30m, Sgt(2026, 10, 1)) };

    var r = Route(InvoiceInputCalculator.Gather([], October, costs, Now), InvoiceDirection.WoodlandsToJb);

    r.KtmbFare.Should().Be(16.30m);
    r.KtmbFareSource.Should().Be(InvoiceFareSource.Override);
  }

  [Fact]
  public void NoTickets_NoFare()
  {
    var r = Route(InvoiceInputCalculator.Gather([], September, [], Now), InvoiceDirection.JbToWoodlands);

    r.KtmbFare.Should().BeNull();
    r.PricedCoverage.Should().BeNull();
    r.MeasuredFare.Should().BeNull();
  }

  // ---- SGT month boundaries ----

  [Fact]
  public void IsClosed_FlipsAtSgtMidnightOfTheNextMonth()
  {
    // 1 Oct 00:00 SGT = 30 Sep 16:00 UTC
    InvoiceInputCalculator.IsClosed(September, Sgt(2026, 9, 30, 23, 59)).Should().BeFalse();
    InvoiceInputCalculator.IsClosed(September, Sgt(2026, 10, 1)).Should().BeTrue();
    // still 30 Sep in UTC, already October in Singapore
    InvoiceInputCalculator.IsClosed(September, new DateTime(2026, 9, 30, 16, 30, 0, DateTimeKind.Utc))
      .Should()
      .BeTrue();
  }

  [Fact]
  public void OnlyTheMonthsDaysCount()
  {
    // SGT-dated rows from the neighbouring months are excluded
    var days = new[]
    {
      Day(new DateOnly(2026, 8, 31), 2, 50, 50, 5000m),
      Day(new DateOnly(2026, 9, 30), 2, 100, 100, 1622.1m),
      Day(new DateOnly(2026, 10, 1), 2, 50, 50, 5000m),
    };

    var r = Route(InvoiceInputCalculator.Gather(days, September, [], Now), InvoiceDirection.WoodlandsToJb);

    r.Tickets.Should().Be(100);
    r.KtmbFare.Should().Be(16.22m);
  }

  [Fact]
  public void ResolveFare_Precedence()
  {
    InvoiceInputCalculator.ResolveFare(17m, 16m, 1m, true).Should().Be((17m, InvoiceFareSource.Override));
    InvoiceInputCalculator.ResolveFare(17m, null, null, false).Should().Be((17m, InvoiceFareSource.Override));
    InvoiceInputCalculator.ResolveFare(null, 16m, 1m, true).Should().Be((16m, InvoiceFareSource.Measured));
    InvoiceInputCalculator.ResolveFare(null, 16m, 1m, false).Should().Be(((decimal?)null, (string?)null));
    InvoiceInputCalculator.ResolveFare(null, 16m, 0.5m, true).Should().Be(((decimal?)null, (string?)null));
    InvoiceInputCalculator.ResolveFare(null, null, null, true).Should().Be(((decimal?)null, (string?)null));
  }
}
