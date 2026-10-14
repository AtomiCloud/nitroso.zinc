using Domain.Booking;
using Domain.Invoice;
using FluentAssertions;

namespace UnitTest.Bookings;

// EBITDA is AFTER the fixed monthly operating cost. The terminal P&L charges
// each month the InvoiceSettings.Infrastructure in force, day-weighted so a
// partial range edge is prorated and a mid-month change is charged each rate
// for the days it applied.
public class PnlTerminalInfrastructureTests
{
  // 00:00 SGT on the given day, as the UTC instant EffectiveAt is stored in
  private static DateTime SgtMidnight(int y, int m, int d) =>
    DateTime.SpecifyKind(new DateTime(y, m, d).AddHours(-8), DateTimeKind.Utc);

  private static InvoiceSettingsChange Infra(decimal amount, DateTime effectiveAt) =>
    new()
    {
      Id = Guid.NewGuid(),
      MarketingSharePct = 50m,
      Infrastructure = amount,
      RecoveryPerBoost = 0m,
      RecoveryPerTicket = 0m,
      EffectiveAt = effectiveAt,
      CreatedAt = effectiveAt,
    };

  private static PnlTerminalDailySum Sale(DateOnly date, decimal collected) =>
    new()
    {
      Date = date,
      Deposits = 0m,
      PaymentFees = 0m,
      PayoutFees = 0m,
      CompletedCount = 1,
      CompletedCollected = collected,
      CompletedKtmbCost = 0m,
      CompletedWithActual = 1,
      TerminatedCount = 0,
      TerminatedCollected = 0m,
      TerminationRefunds = 0m,
      TerminatedKtmbGrossExact = 0m,
      TerminatedKtmbRefund = 0m,
      TerminatedKtmbGrossEstimated = 0m,
      TerminatedWithExactRefund = 0,
      WithdrawalCount = 0,
      WithdrawalGross = 0m,
      WithdrawalFeeIncome = 0m,
    };

  private static readonly InvoiceSettingsChange[] Prod = [Infra(500m, SgtMidnight(2026, 6, 1))];

  // ---- InfrastructureSchedule ----

  [Fact]
  public void A_full_month_under_one_rate_costs_exactly_that_rate()
  {
    var c = InfrastructureSchedule.ForDays(Prod, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

    c.Amount.Should().Be(500m);
    c.Days.Should().Be(31);
    c.DaysInMonth.Should().Be(31);
    c.Source.Should().Be(InfrastructureSource.Settings);
  }

  [Fact]
  public void A_partial_edge_month_is_prorated_by_the_days_covered()
  {
    // 15 of September's 30 days
    var c = InfrastructureSchedule.ForDays(Prod, new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 30));

    c.Amount.Should().Be(250m);
    c.Days.Should().Be(15);
    c.DaysInMonth.Should().Be(30);
  }

  [Fact]
  public void A_mid_month_change_charges_each_rate_for_the_days_it_was_in_force()
  {
    // 500 through 15 Aug, 800 from 00:00 SGT on 16 Aug: 15 days @500 + 16 days @800
    var changes = new[] { Infra(500m, SgtMidnight(2026, 6, 1)), Infra(800m, SgtMidnight(2026, 8, 16)) };

    var c = InfrastructureSchedule.ForDays(changes, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

    // (15×500 + 16×800) / 31 = 20300 / 31 = 654.838… -> 654.84
    c.Amount.Should().Be(654.84m);
    c.Source.Should().Be(InfrastructureSource.Settings);
  }

  [Fact]
  public void A_change_effective_later_on_a_day_applies_from_that_day()
  {
    // effective 15:00 SGT on 16 Aug: in force at the END of the 16th, so the
    // 16th is charged the new rate (the same rule as a month-end lookup)
    var changes = new[]
    {
      Infra(500m, SgtMidnight(2026, 6, 1)),
      Infra(800m, SgtMidnight(2026, 8, 16).AddHours(15)),
    };

    var c = InfrastructureSchedule.ForDays(changes, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

    c.Amount.Should().Be(654.84m);
  }

  [Fact]
  public void No_settings_at_all_charges_nothing_and_says_so()
  {
    var c = InfrastructureSchedule.ForDays([], new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

    c.Amount.Should().Be(0m);
    c.Days.Should().Be(31);
    c.Source.Should().Be(InfrastructureSource.None);
  }

  [Fact]
  public void Days_before_the_first_row_are_charged_at_the_earliest_row_and_flagged()
  {
    // production: the first row took effect 10 Oct 2026, the charge ran from June
    var changes = new[] { Infra(500m, SgtMidnight(2026, 10, 10).AddHours(7)) };

    var aug = InfrastructureSchedule.ForDays(changes, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
    var nov = InfrastructureSchedule.ForDays(changes, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

    aug.Amount.Should().Be(500m);
    aug.Source.Should().Be(InfrastructureSource.Assumed);
    nov.Source.Should().Be(InfrastructureSource.Settings);
  }

  [Fact]
  public void A_queued_future_change_does_not_touch_earlier_days()
  {
    var changes = new[] { Infra(500m, SgtMidnight(2026, 6, 1)), Infra(900m, SgtMidnight(2026, 12, 1)) };

    InfrastructureSchedule
      .ForDays(changes, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))
      .Amount.Should()
      .Be(500m);
  }

  // ---- PnlTerminalCalculator ----

  [Fact]
  public void Every_month_carries_its_own_infrastructure_charge()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [Sale(new DateOnly(2026, 7, 10), 1000m), Sale(new DateOnly(2026, 8, 10), 2000m)],
      new DateOnly(2026, 7, 1),
      new DateOnly(2026, 8, 31),
      Prod,
      new DateOnly(2026, 10, 14)
    );

    rows.Select(r => r.Month).Should().Equal("07-2026", "08-2026");
    rows.Should().AllSatisfy(r => r.Infrastructure.Amount.Should().Be(500m));
  }

  [Fact]
  public void A_month_with_no_activity_still_burns_infrastructure()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [Sale(new DateOnly(2026, 7, 10), 1000m), Sale(new DateOnly(2026, 9, 10), 1000m)],
      new DateOnly(2026, 7, 1),
      new DateOnly(2026, 9, 30),
      Prod,
      new DateOnly(2026, 10, 14)
    );

    rows.Select(r => r.Month).Should().Equal("07-2026", "08-2026", "09-2026");
    var aug = rows[1];
    aug.Completed.Count.Should().Be(0);
    aug.Infrastructure.Amount.Should().Be(500m);
  }

  [Fact]
  public void Range_edges_prorate_the_first_and_last_month()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [],
      new DateOnly(2026, 7, 17),
      new DateOnly(2026, 9, 15),
      Prod,
      new DateOnly(2026, 10, 14)
    );

    // Jul 17-31 = 15/31, Aug full, Sep 1-15 = 15/30
    rows.Select(r => r.Month).Should().Equal("07-2026", "08-2026", "09-2026");
    rows[0].Infrastructure.Amount.Should().Be(241.94m);
    rows[0].Infrastructure.Days.Should().Be(15);
    rows[1].Infrastructure.Amount.Should().Be(500m);
    rows[2].Infrastructure.Amount.Should().Be(250m);
  }

  [Fact]
  public void The_current_month_is_charged_to_date_not_for_days_still_ahead()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [],
      new DateOnly(2026, 10, 1),
      new DateOnly(2026, 10, 31),
      Prod,
      new DateOnly(2026, 10, 14)
    );

    rows.Should().ContainSingle();
    rows[0].Infrastructure.Days.Should().Be(14);
    // 14/31 × 500 = 225.806… -> 225.81
    rows[0].Infrastructure.Amount.Should().Be(225.81m);
  }

  [Fact]
  public void Months_entirely_in_the_future_are_not_emitted()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [],
      new DateOnly(2026, 11, 1),
      new DateOnly(2026, 12, 31),
      Prod,
      new DateOnly(2026, 10, 14)
    );

    rows.Should().BeEmpty();
  }

  [Fact]
  public void An_open_range_runs_from_the_first_activity_to_today()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [Sale(new DateOnly(2026, 8, 20), 100m)],
      null,
      null,
      Prod,
      new DateOnly(2026, 10, 14)
    );

    // Aug prorated from the first activity day (20-31 = 12/31), Sep full, Oct to date
    rows.Select(r => r.Month).Should().Equal("08-2026", "09-2026", "10-2026");
    rows[0].Infrastructure.Days.Should().Be(12);
    rows[1].Infrastructure.Amount.Should().Be(500m);
    rows[2].Infrastructure.Days.Should().Be(14);
  }

  [Fact]
  public void No_settings_rows_emit_only_activity_months_at_zero_infrastructure()
  {
    var rows = PnlTerminalCalculator.Analyze(
      [Sale(new DateOnly(2026, 7, 10), 1000m)],
      new DateOnly(2026, 7, 1),
      new DateOnly(2026, 9, 30),
      [],
      new DateOnly(2026, 10, 14)
    );

    rows.Should().ContainSingle();
    rows[0].Infrastructure.Amount.Should().Be(0m);
    rows[0].Infrastructure.Source.Should().Be(InfrastructureSource.None);
  }
}
