using Domain.User;
using FluentAssertions;

namespace UnitTest.Users;

// Pure rollup contract: sparse SGT daily source totals merge into
// chronological, non-empty calendar months with inclusive bounds.
public class PartnerPnlCalculatorTests
{
  private static PartnerPnlDailySum Day(
    DateOnly? date = null,
    int bookings = 0,
    decimal collected = 0m,
    decimal ktmbCost = 0m,
    decimal deposits = 0m,
    decimal withdrawalGross = 0m,
    decimal withdrawalFeeIncome = 0m,
    int boostCount = 0,
    decimal boostAmount = 0m,
    int distinctPassengers = 0
  ) =>
    new()
    {
      Date = date ?? new DateOnly(2026, 8, 1),
      Bookings = bookings,
      Collected = collected,
      KtmbCost = ktmbCost,
      Deposits = deposits,
      WithdrawalGross = withdrawalGross,
      WithdrawalFeeIncome = withdrawalFeeIncome,
      BoostCount = boostCount,
      BoostAmount = boostAmount,
      DistinctPassengers = distinctPassengers,
    };

  [Fact]
  public void Sparse_sources_in_the_same_month_merge_all_measures()
  {
    var rows = PartnerPnlCalculator.Analyze(
      [
        Day(new DateOnly(2026, 8, 1), deposits: 200m),
        Day(
          new DateOnly(2026, 8, 7),
          withdrawalGross: 90m,
          withdrawalFeeIncome: 4m
        ),
        Day(
          new DateOnly(2026, 8, 31),
          bookings: 3,
          collected: 135m,
          ktmbCost: 61.2m,
          boostCount: 2,
          boostAmount: 9.9m,
          distinctPassengers: 3
        ),
      ],
      null,
      null
    );

    rows.Should().ContainSingle();
    rows[0]
      .Should()
      .BeEquivalentTo(
        new PartnerPnlRow
        {
          Month = "08-2026",
          Bookings = 3,
          Collected = 135m,
          KtmbCost = 61.2m,
          Deposits = 200m,
          WithdrawalGross = 90m,
          WithdrawalFeeIncome = 4m,
          BoostCount = 2,
          BoostAmount = 9.9m,
          DistinctPassengers = 3,
        }
      );
  }

  [Fact]
  public void Inclusive_bounds_filter_before_month_bucketing_and_months_sort_chronologically()
  {
    var rows = PartnerPnlCalculator.Analyze(
      [
        Day(new DateOnly(2026, 7, 31), deposits: 1m),
        Day(new DateOnly(2027, 1, 1), deposits: 4m),
        Day(new DateOnly(2026, 8, 1), deposits: 2m),
        Day(new DateOnly(2026, 8, 31), deposits: 3m),
        Day(new DateOnly(2026, 9, 1), bookings: 1),
      ],
      new DateOnly(2026, 8, 1),
      new DateOnly(2027, 1, 1)
    );

    rows.Select(r => r.Month).Should().Equal("08-2026", "09-2026", "01-2027");
    rows[0].Deposits.Should().Be(5m);
    rows[1].Bookings.Should().Be(1, "a completed zero-price booking still makes the month non-empty");
  }

  [Fact]
  public void Free_boost_consumption_remains_visible_when_the_amount_paid_is_zero()
  {
    // The repository normalizes a FREE boost's null PriorityFee snapshot to
    // zero, but Priority = true still contributes one consumed boost.
    var rows = PartnerPnlCalculator.Analyze(
      [Day(bookings: 1, boostCount: 1, boostAmount: 0m)],
      null,
      null
    );

    rows.Should().ContainSingle();
    rows[0].BoostCount.Should().Be(1);
    rows[0].BoostAmount.Should().Be(0m);
  }

  [Fact]
  public void Zero_only_months_are_omitted()
  {
    var rows = PartnerPnlCalculator.Analyze([Day()], null, null);

    rows.Should().BeEmpty();
  }
}
