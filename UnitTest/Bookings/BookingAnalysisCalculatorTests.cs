using Domain.Booking;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Bookings;

// The pure summary math behind GET Booking/analysis: totals are sums over
// the per-day rows; priority fees are net of refunds; termination fees are
// the collected cost of terminated bookings minus what was refunded; gateway
// fees and the per-direction split pass through from their own aggregations;
// the monthly rollup nets gross against KTMB cost and gateway fees only
// (internal fees are revenue, never subtracted).
public class BookingAnalysisCalculatorTests
{
  private static BookingAnalysisRow Row(
    int tickets,
    decimal gross,
    TrainDirection direction = TrainDirection.JToW,
    decimal ktmb = 0m,
    DateOnly? date = null
  ) =>
    new()
    {
      Date = date ?? new DateOnly(2026, 7, 1),
      Direction = direction,
      Time = new TimeOnly(8, 30),
      TicketsCompleted = tickets,
      GrossRevenue = gross,
      KtmbCost = ktmb,
    };

  private static readonly DepositSummary NoDeposits = new() { Count = 0, Captured = 0m };

  private static readonly BookingAnalysisLedgerSums ZeroSums = new()
  {
    DepositFees = 0m,
    WithdrawalFees = 0m,
    PriorityFeesCharged = 0m,
    PriorityFeesRefunded = 0m,
    TerminatedGross = 0m,
    TerminationRefunds = 0m,
  };

  private static readonly GatewayFeeSummary NoGatewayFees = new()
  {
    Payments = 0m,
    Payouts = 0m,
    Coverage = new GatewayFeeCoverage { PaymentsWithFee = 0, PaymentsTotal = 0 },
  };

  private static readonly KtmbActualCoverage NoKtmbCoverage = new() { WithActual = 0, Total = 0 };

  private static readonly Dictionary<string, MonthlyExternalSums> NoExternal = new();

  [Fact]
  public void Totals_sum_across_rows()
  {
    var rows = new[]
    {
      Row(3, 42m, ktmb: 30m),
      Row(2, 28m, TrainDirection.WToJ, ktmb: 20m),
      Row(5, 80m, ktmb: 50m),
    };

    var s = BookingAnalysisCalculator.Summarize(rows, NoDeposits, ZeroSums, NoGatewayFees, NoKtmbCoverage);

    s.TotalTickets.Should().Be(10);
    s.TotalGross.Should().Be(150m);
    s.TotalKtmbCost.Should().Be(100m);
  }

  [Fact]
  public void Empty_rows_produce_zero_totals()
  {
    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, ZeroSums, NoGatewayFees, NoKtmbCoverage);

    s.TotalTickets.Should().Be(0);
    s.TotalGross.Should().Be(0m);
    s.TotalKtmbCost.Should().Be(0m);
    s.ByDirection.Should().BeEmpty();
  }

  [Fact]
  public void Deposits_pass_through_unchanged()
  {
    var deposits = new DepositSummary { Count = 7, Captured = 350.5m };

    var s = BookingAnalysisCalculator.Summarize([], deposits, ZeroSums, NoGatewayFees, NoKtmbCoverage);

    s.Deposits.Count.Should().Be(7);
    s.Deposits.Captured.Should().Be(350.5m);
  }

  [Fact]
  public void Deposit_and_withdrawal_fees_pass_through()
  {
    var sums = ZeroSums with { DepositFees = 12.5m, WithdrawalFees = 4m };

    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, sums, NoGatewayFees, NoKtmbCoverage);

    s.InternalFees.Deposit.Should().Be(12.5m);
    s.InternalFees.Withdrawal.Should().Be(4m);
  }

  [Fact]
  public void Priority_fee_is_net_of_refunds()
  {
    // both charge and refund rows share TransactionType.PriorityFee; the
    // net is what BunnyBooker actually kept
    var sums = ZeroSums with { PriorityFeesCharged = 50m, PriorityFeesRefunded = 20m };

    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, sums, NoGatewayFees, NoKtmbCoverage);

    s.InternalFees.Priority.Should().Be(30m);
  }

  [Fact]
  public void Termination_fee_is_collected_minus_refunded()
  {
    // the BookingTerminated ledger row only carries the refund; the kept fee
    // is the terminated bookings' collected cost minus that refund
    var sums = ZeroSums with { TerminatedGross = 100m, TerminationRefunds = 60m };

    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, sums, NoGatewayFees, NoKtmbCoverage);

    s.InternalFees.Termination.Should().Be(40m);
  }

  [Fact]
  public void Gateway_fees_pass_through_unchanged()
  {
    var gateway = new GatewayFeeSummary
    {
      Payments = 12.34m,
      Payouts = 5.67m,
      Coverage = new GatewayFeeCoverage { PaymentsWithFee = 8, PaymentsTotal = 10 },
    };

    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, ZeroSums, gateway, NoKtmbCoverage);

    s.GatewayFees.Payments.Should().Be(12.34m);
    s.GatewayFees.Payouts.Should().Be(5.67m);
    s.GatewayFees.Coverage.PaymentsWithFee.Should().Be(8);
    s.GatewayFees.Coverage.PaymentsTotal.Should().Be(10);
  }

  [Fact]
  public void Ktmb_actual_coverage_passes_through_unchanged()
  {
    // tin records/backfills the actual paid amount with delay — the summary
    // reports capture coverage exactly like the gateway-fee coverage
    var coverage = new KtmbActualCoverage { WithActual = 3, Total = 9 };

    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, ZeroSums, NoGatewayFees, coverage);

    s.KtmbActualCoverage.WithActual.Should().Be(3);
    s.KtmbActualCoverage.Total.Should().Be(9);
  }

  [Fact]
  public void By_direction_splits_tickets_gross_and_ktmb()
  {
    var rows = new[]
    {
      Row(3, 42m, ktmb: 30m),
      Row(2, 28m, TrainDirection.WToJ, ktmb: 16m),
      Row(5, 80m, ktmb: 50m),
    };

    var s = BookingAnalysisCalculator.Summarize(rows, NoDeposits, ZeroSums, NoGatewayFees, NoKtmbCoverage);

    s.ByDirection.Should().HaveCount(2);
    var jtow = s.ByDirection.Single(d => d.Direction == TrainDirection.JToW);
    jtow.Tickets.Should().Be(8);
    jtow.Gross.Should().Be(122m);
    jtow.KtmbCost.Should().Be(80m);
    var wtoj = s.ByDirection.Single(d => d.Direction == TrainDirection.WToJ);
    wtoj.Tickets.Should().Be(2);
    wtoj.Gross.Should().Be(28m);
    wtoj.KtmbCost.Should().Be(16m);
  }

  [Fact]
  public void Full_summary_combines_all_parts()
  {
    var rows = new[] { Row(4, 64m, ktmb: 40m) };
    var deposits = new DepositSummary { Count = 2, Captured = 100m };
    var sums = new BookingAnalysisLedgerSums
    {
      DepositFees = 1m,
      WithdrawalFees = 2m,
      PriorityFeesCharged = 30m,
      PriorityFeesRefunded = 10m,
      TerminatedGross = 16m,
      TerminationRefunds = 8m,
    };

    var s = BookingAnalysisCalculator.Summarize(rows, deposits, sums, NoGatewayFees, NoKtmbCoverage);

    s.TotalTickets.Should().Be(4);
    s.TotalGross.Should().Be(64m);
    s.TotalKtmbCost.Should().Be(40m);
    s.Deposits.Count.Should().Be(2);
    s.Deposits.Captured.Should().Be(100m);
    s.InternalFees.Deposit.Should().Be(1m);
    s.InternalFees.Withdrawal.Should().Be(2m);
    s.InternalFees.Priority.Should().Be(20m);
    s.InternalFees.Termination.Should().Be(8m);
  }

  // ---- monthly rollup ----

  [Fact]
  public void Monthly_net_is_gross_minus_ktmb_and_gateway_fees()
  {
    var rows = new[] { Row(10, 200m, ktmb: 130m, date: new DateOnly(2026, 7, 5)) };
    var external = new Dictionary<string, MonthlyExternalSums>
    {
      ["07-2026"] = new()
      {
        GatewayPaymentFees = 6m,
        GatewayPayoutFees = 4m,
        InternalFees = 25m,
      },
    };

    var m = BookingAnalysisCalculator.MonthlyRollup(rows, external);

    m.Should().HaveCount(1);
    m[0].Month.Should().Be("07-2026");
    m[0].Gross.Should().Be(200m);
    m[0].KtmbCost.Should().Be(130m);
    m[0].GatewayPaymentFees.Should().Be(6m);
    m[0].GatewayPayoutFees.Should().Be(4m);
    // internal fees are revenue TO us: reported, NOT subtracted
    m[0].InternalFees.Should().Be(25m);
    m[0].Net.Should().Be(200m - 130m - 6m - 4m);
  }

  [Fact]
  public void Monthly_rollup_buckets_by_calendar_month_and_sorts_chronologically()
  {
    var rows = new[]
    {
      Row(1, 20m, date: new DateOnly(2026, 8, 1)),
      Row(1, 10m, date: new DateOnly(2026, 7, 31)),
      Row(1, 30m, date: new DateOnly(2025, 12, 31)),
    };

    var m = BookingAnalysisCalculator.MonthlyRollup(rows, NoExternal);

    m.Select(x => x.Month).Should().Equal("12-2025", "07-2026", "08-2026");
    m.Single(x => x.Month == "07-2026").Gross.Should().Be(10m);
    m.Single(x => x.Month == "08-2026").Gross.Should().Be(20m);
  }

  [Fact]
  public void Monthly_rollup_emits_fee_only_months_with_zero_gross()
  {
    // a month with gateway/internal fee movement but no completed bookings
    // must still be reported (negative net = pure cost month)
    var external = new Dictionary<string, MonthlyExternalSums>
    {
      ["06-2026"] = new()
      {
        GatewayPaymentFees = 3m,
        GatewayPayoutFees = 1m,
        InternalFees = 0m,
      },
    };

    var m = BookingAnalysisCalculator.MonthlyRollup([], external);

    m.Should().HaveCount(1);
    m[0].Month.Should().Be("06-2026");
    m[0].Gross.Should().Be(0m);
    m[0].Net.Should().Be(-4m);
    m[0].ByDirection.Should().BeEmpty();
  }

  [Fact]
  public void Monthly_rollup_carries_the_direction_split_per_month()
  {
    var rows = new[]
    {
      Row(2, 30m, ktmb: 20m, date: new DateOnly(2026, 7, 1)),
      Row(1, 15m, TrainDirection.WToJ, ktmb: 8m, date: new DateOnly(2026, 7, 2)),
      Row(4, 60m, ktmb: 40m, date: new DateOnly(2026, 8, 1)),
    };

    var m = BookingAnalysisCalculator.MonthlyRollup(rows, NoExternal);

    var july = m.Single(x => x.Month == "07-2026");
    july.ByDirection.Should().HaveCount(2);
    july.ByDirection.Single(d => d.Direction == TrainDirection.JToW).Gross.Should().Be(30m);
    july.ByDirection.Single(d => d.Direction == TrainDirection.WToJ).KtmbCost.Should().Be(8m);
    var august = m.Single(x => x.Month == "08-2026");
    august.ByDirection.Should().HaveCount(1);
    august.ByDirection.Single().Tickets.Should().Be(4);
  }

  [Fact]
  public void Zero_ktmb_rows_net_against_gateway_fees_only()
  {
    // KTMB cost never configured -> every row carries 0 and net = gross −
    // gateway fees (documented response behavior)
    var rows = new[] { Row(5, 100m, date: new DateOnly(2026, 7, 1)) };
    var external = new Dictionary<string, MonthlyExternalSums>
    {
      ["07-2026"] = new()
      {
        GatewayPaymentFees = 2m,
        GatewayPayoutFees = 1m,
        InternalFees = 0m,
      },
    };

    var m = BookingAnalysisCalculator.MonthlyRollup(rows, external);

    m[0].KtmbCost.Should().Be(0m);
    m[0].Net.Should().Be(97m);
  }
}
