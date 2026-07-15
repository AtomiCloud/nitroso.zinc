using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// Pure terminal-event P&L math: SGT daily subtotals merge across sources,
// bounds are inclusive, months are chronological, the blended gateway rate
// and the KTMB refund fallback are applied per month, and no zero-only month
// leaks to the wire. Source-specific attribution (collected = request amount
// + kept boost fee) and FX conversion happen in SQL.
public class PnlTerminalCalculatorTests
{
  private static PnlTerminalDailySum Day(
    DateOnly? date = null,
    decimal deposits = 0m,
    decimal paymentFees = 0m,
    decimal payoutFees = 0m,
    int completedCount = 0,
    decimal completedCollected = 0m,
    decimal completedKtmbCost = 0m,
    int terminatedCount = 0,
    decimal terminatedCollected = 0m,
    decimal terminationRefunds = 0m,
    decimal terminatedKtmbGrossExact = 0m,
    decimal terminatedKtmbRefund = 0m,
    decimal terminatedKtmbGrossEstimated = 0m,
    int terminatedWithExactRefund = 0,
    int withdrawalCount = 0,
    decimal withdrawalGross = 0m,
    decimal withdrawalFeeIncome = 0m
  ) =>
    new()
    {
      Date = date ?? new DateOnly(2026, 8, 1),
      Deposits = deposits,
      PaymentFees = paymentFees,
      PayoutFees = payoutFees,
      CompletedCount = completedCount,
      CompletedCollected = completedCollected,
      CompletedKtmbCost = completedKtmbCost,
      TerminatedCount = terminatedCount,
      TerminatedCollected = terminatedCollected,
      TerminationRefunds = terminationRefunds,
      TerminatedKtmbGrossExact = terminatedKtmbGrossExact,
      TerminatedKtmbRefund = terminatedKtmbRefund,
      TerminatedKtmbGrossEstimated = terminatedKtmbGrossEstimated,
      TerminatedWithExactRefund = terminatedWithExactRefund,
      WithdrawalCount = withdrawalCount,
      WithdrawalGross = withdrawalGross,
      WithdrawalFeeIncome = withdrawalFeeIncome,
    };

  [Fact]
  public void Days_and_sparse_sources_in_the_same_month_merge_all_measures()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [
        Day(new DateOnly(2026, 8, 1), deposits: 300m),
        Day(new DateOnly(2026, 8, 3), paymentFees: 7m, payoutFees: 2.35m),
        Day(
          new DateOnly(2026, 8, 7),
          completedCount: 3,
          completedCollected: 135.5m,
          completedKtmbCost: 60.25m
        ),
        Day(
          new DateOnly(2026, 8, 12),
          terminatedCount: 1,
          terminatedCollected: 45m,
          terminatedKtmbGrossExact: 20m,
          terminatedKtmbRefund: 8m,
          terminatedWithExactRefund: 1
        ),
        Day(new DateOnly(2026, 8, 12), terminationRefunds: 22.5m),
        Day(
          new DateOnly(2026, 8, 31),
          withdrawalCount: 4,
          withdrawalGross: 400m,
          withdrawalFeeIncome: 16m
        ),
      ],
      null,
      null
    );

    rows.Should().ContainSingle();
    rows[0]
      .Should()
      .BeEquivalentTo(
        new PnlTerminalRow
        {
          Month = "08-2026",
          Deposits = 300m,
          PaymentFees = 7m,
          GwRate = 0.023333m,
          Completed = new PnlTerminalCompleted
          {
            Count = 3,
            Collected = 135.5m,
            KtmbCost = 60.25m,
          },
          Terminated = new PnlTerminalTerminated
          {
            Count = 1,
            Kept = 22.5m,
            KtmbCostNet = 12m,
            WithExactRefund = 1,
          },
          Withdrawals = new PnlTerminalWithdrawals
          {
            Count = 4,
            Gross = 400m,
            FeeIncome = 16m,
            PayoutFees = 2.35m,
          },
        }
      );
  }

  [Fact]
  public void Gw_rate_is_payment_fees_over_deposits_rounded_to_6dp()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [Day(deposits: 300m, paymentFees: 7m)],
      null,
      null
    );

    // 7 / 300 = 0.02333... -> 6dp
    rows[0].GwRate.Should().Be(0.023333m);
  }

  [Fact]
  public void Gw_rate_is_zero_when_the_month_has_no_deposits()
  {
    var rows = PnlTerminalCalculator.Analyze([Day(paymentFees: 5m)], null, null);

    rows[0].GwRate.Should().Be(0m);
  }

  [Fact]
  public void Kept_is_collected_minus_the_ledger_refunds()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [
        Day(terminatedCount: 2, terminatedCollected: 100m),
        Day(terminationRefunds: 70m),
      ],
      null,
      null
    );

    rows[0].Terminated.Kept.Should().Be(30m);
  }

  [Fact]
  public void Ktmb_cost_net_uses_exact_refunds_and_the_fallback_for_uncaptured_ones()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [
        Day(
          terminatedCount: 3,
          terminatedCollected: 90m,
          // 2 with a captured refund: gross 100 - refund 40 = 60 exact
          terminatedKtmbGrossExact: 100m,
          terminatedKtmbRefund: 40m,
          terminatedWithExactRefund: 2,
          // 1 without: gross 80 costed at the 0.50 fallback = 40
          terminatedKtmbGrossEstimated: 80m
        ),
      ],
      null,
      null
    );

    rows[0].Terminated.KtmbCostNet.Should().Be(100m);
    rows[0].Terminated.WithExactRefund.Should().Be(2);
  }

  [Fact]
  public void The_ktmb_refund_fallback_rate_is_a_single_named_constant_at_50_percent()
  {
    PnlTerminalCalculator.KtmbRefundFallbackRate.Should().Be(0.50m);
  }

  [Fact]
  public void Inclusive_date_bounds_filter_before_month_bucketing()
  {
    var rows = PnlTerminalCalculator.Analyze(
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
    var rows = PnlTerminalCalculator.Analyze(
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
  public void A_month_with_only_one_source_keeps_its_other_measures_at_zero()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [Day(withdrawalCount: 1, withdrawalGross: 50m, withdrawalFeeIncome: 2m)],
      null,
      null
    );

    rows.Should().ContainSingle();
    rows[0].Deposits.Should().Be(0m);
    rows[0].GwRate.Should().Be(0m);
    rows[0].Completed.Count.Should().Be(0);
    rows[0].Terminated.Count.Should().Be(0);
    rows[0].Withdrawals.Gross.Should().Be(50m);
  }

  [Fact]
  public void Empty_or_zero_only_inputs_produce_no_months()
  {
    PnlTerminalCalculator.Analyze([], null, null).Should().BeEmpty();
    PnlTerminalCalculator.Analyze([Day()], null, null).Should().BeEmpty();
  }
}
