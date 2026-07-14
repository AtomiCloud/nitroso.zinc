using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// Pure monthly P&L math: SGT daily subtotals merge across sources, bounds
// are inclusive, months are chronological, and no zero-only month leaks to
// the wire. Source-specific attribution and FX conversion happen in SQL.
public class PnlAnalysisCalculatorTests
{
  private static PnlAnalysisDailySum Day(
    DateOnly? date = null,
    decimal deposits = 0m,
    int withdrawalCount = 0,
    decimal withdrawalTotal = 0m,
    decimal withdrawalFeeIncome = 0m,
    decimal gatewayFees = 0m,
    decimal ticketRevenue = 0m,
    decimal ktmbCost = 0m
  ) =>
    new()
    {
      Date = date ?? new DateOnly(2026, 8, 1),
      Deposits = deposits,
      WithdrawalCount = withdrawalCount,
      WithdrawalTotal = withdrawalTotal,
      WithdrawalFeeIncome = withdrawalFeeIncome,
      GatewayFees = gatewayFees,
      TicketRevenue = ticketRevenue,
      KtmbCost = ktmbCost,
    };

  [Fact]
  public void Days_and_sparse_sources_in_the_same_month_merge_all_measures()
  {
    var rows = PnlAnalysisCalculator.Analyze(
      [
        Day(new DateOnly(2026, 8, 1), deposits: 200m),
        Day(
          new DateOnly(2026, 8, 7),
          withdrawalCount: 2,
          withdrawalTotal: 90m,
          withdrawalFeeIncome: 4m
        ),
        Day(new DateOnly(2026, 8, 7), gatewayFees: 3.25m),
        Day(new DateOnly(2026, 8, 31), ticketRevenue: 135m, ktmbCost: 61.2m),
      ],
      null,
      null
    );

    rows.Should().ContainSingle();
    rows[0]
      .Should()
      .BeEquivalentTo(
        new PnlAnalysisRow
        {
          Month = "08-2026",
          Deposits = 200m,
          WithdrawalCount = 2,
          WithdrawalTotal = 90m,
          WithdrawalFeeIncome = 4m,
          GatewayFees = 3.25m,
          TicketRevenue = 135m,
          KtmbCost = 61.2m,
        }
      );
  }

  [Fact]
  public void Withdrawal_total_stays_gross_and_fee_income_is_separate()
  {
    var rows = PnlAnalysisCalculator.Analyze(
      [Day(withdrawalCount: 1, withdrawalTotal: 100m, withdrawalFeeIncome: 2m)],
      null,
      null
    );

    rows[0].WithdrawalTotal.Should().Be(100m);
    rows[0].WithdrawalFeeIncome.Should().Be(2m);
  }

  [Fact]
  public void Inclusive_date_bounds_filter_before_month_bucketing()
  {
    var rows = PnlAnalysisCalculator.Analyze(
      [
        Day(new DateOnly(2026, 7, 31), deposits: 1m),
        Day(new DateOnly(2026, 8, 1), deposits: 2m),
        Day(new DateOnly(2026, 8, 31), deposits: 3m),
        Day(new DateOnly(2026, 9, 1), deposits: 4m),
      ],
      new DateOnly(2026, 8, 1),
      new DateOnly(2026, 8, 31)
    );

    rows.Should().ContainSingle();
    rows[0].Month.Should().Be("08-2026");
    rows[0].Deposits.Should().Be(5m);
  }

  [Fact]
  public void Null_bounds_are_unbounded_and_months_sort_chronologically()
  {
    var rows = PnlAnalysisCalculator.Analyze(
      [
        Day(new DateOnly(2027, 1, 1), deposits: 1m),
        Day(new DateOnly(2026, 12, 1), deposits: 1m),
        Day(new DateOnly(2026, 2, 1), deposits: 1m),
      ],
      null,
      null
    );

    rows.Select(r => r.Month).Should().Equal("02-2026", "12-2026", "01-2027");
  }

  [Fact]
  public void A_month_with_other_activity_keeps_zero_gateway_fees()
  {
    var rows = PnlAnalysisCalculator.Analyze([Day(ticketRevenue: 45m)], null, null);

    rows.Should().ContainSingle();
    rows[0].GatewayFees.Should().Be(0m);
  }

  [Fact]
  public void Empty_or_zero_only_inputs_produce_no_months()
  {
    PnlAnalysisCalculator.Analyze([], null, null).Should().BeEmpty();
    PnlAnalysisCalculator.Analyze([Day()], null, null).Should().BeEmpty();
  }
}
