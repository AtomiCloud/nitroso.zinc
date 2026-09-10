using Domain.Invoice;
using FluentAssertions;
using FluentAssertions.Execution;

namespace UnitTest.Invoices;

// Properties that must hold for ANY month, not just the three we have issued.
//
// The golden fixtures prove we reproduce the past. These prove the engine will
// not do something absurd on a month nobody has seen yet — which is the whole
// point of moving generation onto the web, where months will be computed
// without anyone checking them by hand first.
public class InvoiceCalculatorInvariantTests
{
  public static TheoryData<string> Months => new() { "2026-06", "2026-07", "2026-08" };

  // ---- allocation ---------------------------------------------------------

  [Theory]
  [MemberData(nameof(Months))]
  public void The_shares_always_sum_to_the_pool(string month)
  {
    // Largest-remainder allocation exists for exactly this: independently
    // rounded shares can drift a cent off the pool in either direction.
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    c.Result.Shares.Sum(x => x.Earned).Should().Be(c.Result.MarketingSharePool);
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void The_advances_always_sum_to_the_recovery_total(string month)
  {
    // If they did not, we would be withholding more or less than the partner
    // is actually holding, and the invoice would not reconcile against cash.
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    c.Result.Shares.Sum(x => x.Advance).Should().Be(c.Recovery.Total);
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void What_we_pay_is_what_they_earned_less_what_they_hold(string month)
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    using var _ = new AssertionScope();
    foreach (var s in c.Result.Shares)
      s.Amount.Should().Be(s.Earned - s.Advance, "{0}", s.Suffix);
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void No_partner_is_shorted_more_than_a_cent_against_an_even_split(string month)
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    var even = c.Result.MarketingSharePool / c.Result.Shares.Length;
    using var _ = new AssertionScope();
    foreach (var s in c.Result.Shares)
      Math.Abs(s.Earned - even).Should().BeLessThanOrEqualTo(0.01m, "{0}", s.Suffix);
  }

  // ---- the recovery must LOWER the payable -------------------------------

  [Fact]
  public void The_recovery_lowers_the_payable_by_its_full_amount()
  {
    // The mistake this pins against is real and was made once: deducting the
    // recovery from the pool BEFORE the split claws back only half of it, and
    // the payable then goes UP relative to an invoice with no recovery at all.
    // The payable must DROP by exactly what the partner is holding.
    var withRecovery = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-08"));
    var without = InvoiceCalculator.Compute(
      InvoiceFixture.Input("2026-08") with { PartnerRecovery = null });

    using var _ = new AssertionScope();

    // the profit split itself is untouched — the recovery is not a cost
    withRecovery.Result.NetProfit.Should().Be(without.Result.NetProfit);
    withRecovery.Result.MarketingSharePool.Should().Be(without.Result.MarketingSharePool);
    withRecovery.Result.Shares.Sum(x => x.Earned).Should().Be(without.Result.Shares.Sum(x => x.Earned));

    // ...but the total paid out drops by the FULL recovery, not half of it
    var dropped = without.Result.Shares.Sum(x => x.Amount) - withRecovery.Result.Shares.Sum(x => x.Amount);
    dropped.Should().Be(withRecovery.Recovery.Total);
  }

  [Fact]
  public void Recovery_does_not_apply_when_there_is_nothing_to_recover()
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-06"));
    using var _ = new AssertionScope();
    c.Recovery.Total.Should().Be(0m);
    c.Recovery.Applies.Should().BeFalse();
    c.Result.Shares.Should().AllSatisfy(s => s.Amount.Should().Be(s.Earned));
  }

  // ---- ancillary ----------------------------------------------------------

  [Theory]
  [MemberData(nameof(Months))]
  public void Priority_fees_reconcile_against_the_boost_count(string month)
  {
    // A boost is a flat SGD 10.00. If gross ever stops being paid x 10, either
    // the price changed or we are counting the wrong bookings.
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    c.Ancillary.Priority.Gross.Should().Be(c.Ancillary.Priority.Paid * 10m);
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void Priority_fees_kept_on_cancellation_reconcile_against_their_count(string month)
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    c.Ancillary.Priority.Kept.Should().Be(c.Ancillary.Priority.KeptCount * 10m);
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void Surcharges_are_never_added_to_revenue(string month)
  {
    // They are already inside the booking price. Adding them would
    // double-count. Revenue must be exactly the sum of the route revenues.
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    c.Totals.Revenue.Should().Be(InvoiceRounding.R(c.Routes.Sum(x => x.Revenue)));
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void Discounts_are_never_positive(string month)
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    c.Ancillary.Surcharge.Discounts.Should().BeLessThanOrEqualTo(0m);
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void Ancillary_total_is_the_additive_lines_only(string month)
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    var a = c.Ancillary;
    a.Total.Should().Be(InvoiceRounding.R(
      a.Priority.Net + a.Priority.Kept + a.WithdrawalFee.Income - a.Promotional - a.NetTransfers));
  }

  // ---- the P&L identity ---------------------------------------------------

  [Theory]
  [MemberData(nameof(Months))]
  public void Net_profit_is_contribution_plus_adjustments_less_costs(string month)
  {
    // The user's question — "does the net profit take in the adjustment or
    // not?" — pinned as an assertion so the answer can never quietly change.
    // It does. All of them.
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    c.Result.NetProfit.Should().Be(InvoiceRounding.R(
      c.Totals.Contribution
      + c.Adjustments.TerminatedNet
      + c.Ancillary.Total
      - c.Adjustments.WastedFee
      - c.Adjustments.Infrastructure));
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void Direct_cost_is_the_fare_plus_the_processing_fee(string month)
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    using var _ = new AssertionScope();
    foreach (var rt in c.Routes)
      rt.DirectCost.Should().Be(InvoiceRounding.R(rt.TicketFaresSgd + rt.ProcessingFee), "{0}", rt.Key);
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void The_blended_price_per_ticket_is_revenue_over_tickets(string month)
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    c.Totals.PricePerTicket.Should().Be(InvoiceRounding.R(c.Totals.Revenue / c.Totals.Tickets));
  }

  [Theory]
  [MemberData(nameof(Months))]
  public void Totals_are_sums_of_the_rounded_route_figures(string month)
  {
    // Not a re-derivation from raw inputs. This is what makes the TOTAL column
    // reconcile against the two route columns on the printed page.
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    using var _ = new AssertionScope();
    c.Totals.TicketFaresSgd.Should().Be(InvoiceRounding.R(c.Routes.Sum(x => x.TicketFaresSgd)));
    c.Totals.ProcessingFee.Should().Be(InvoiceRounding.R(c.Routes.Sum(x => x.ProcessingFee)));
    c.Totals.DirectCost.Should().Be(InvoiceRounding.R(c.Routes.Sum(x => x.DirectCost)));
    c.Totals.Contribution.Should().Be(InvoiceRounding.R(c.Routes.Sum(x => x.Contribution)));
  }

  // ---- overrides ----------------------------------------------------------

  [Fact]
  public void An_override_is_honoured_but_the_formula_result_stays_visible()
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-06"));
    using var _ = new AssertionScope();
    c.Adjustments.WastedFee.Should().Be(58.42m, "the issued document");
    c.Adjustments.WastedFeeComputed.Should().Be(58.41m, "the formula");
    c.Adjustments.WastedFee.Should().NotBe(c.Adjustments.WastedFeeComputed,
      "the variance must stay reportable, not be absorbed");
  }

  [Fact]
  public void Without_an_override_the_formula_stands()
  {
    var c = InvoiceCalculator.Compute(
      InvoiceFixture.Input("2026-06") with { WastedFeeOverride = null });
    c.Adjustments.WastedFee.Should().Be(c.Adjustments.WastedFeeComputed);
  }

  // ---- degenerate months --------------------------------------------------

  [Fact]
  public void An_empty_month_computes_to_zero_rather_than_throwing()
  {
    // The web generator will be pointed at a month before it is over, and at
    // months with no activity at all. Every divisor in the engine is guarded;
    // this is what proves it. The TypeScript returned NaN here.
    var empty = InvoiceFixture.Input("2026-06") with
    {
      Topups = [],
      Routes = [],
      GrossDeposits = 0m,
      Fees = new InvoiceFeesInput { Gateway = 0m, PaymentMethod = 0m },
      Withdrawals = new InvoiceWithdrawalsInput { Count = 0, Total = 0m },
      Infrastructure = 0m,
      Priority = new InvoicePriorityInput
      {
        PerRoute = new Dictionary<string, InvoicePriorityRouteInput>(),
        KeptOnCancelled = 0m,
        KeptOnCancelledCount = 0,
      },
      Surcharge = new InvoiceSurchargeInput
      {
        Coverage = new InvoiceSurchargeCoverage { WithBreakdown = 0, Total = 0 },
        PerRoute = new Dictionary<string, InvoicePriceLine[]>(),
      },
      WithdrawalFee = new InvoiceWithdrawalFeeInput { Income = 0m, WithFee = 0, Count = 0 },
      Promotional = new InvoicePromotionalInput { Count = 0, Amount = 0m },
      NetTransfers = 0m,
      FeeRateOverride = null,
      WastedFeeOverride = null,
    };

    var c = InvoiceCalculator.Compute(empty);

    using var _ = new AssertionScope();
    c.Fx.FxRate.Should().Be(0m);
    c.Fee.FeeRatePct.Should().Be(0m);
    c.Totals.Revenue.Should().Be(0m);
    c.Totals.PricePerTicket.Should().Be(0m);
    c.Result.NetProfit.Should().Be(0m);
    c.Result.Shares.Should().AllSatisfy(s => s.Amount.Should().Be(0m));
  }

  [Fact]
  public void A_month_with_no_partners_does_not_throw()
  {
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-08") with { Partners = [] });
    using var _ = new AssertionScope();
    c.Result.Shares.Should().BeEmpty();
    // the pool is still computed — it just has nobody to go to
    c.Result.MarketingSharePool.Should().Be(18735.76m);
  }

  [Fact]
  public void A_loss_making_month_splits_the_loss_rather_than_hiding_it()
  {
    // Infrastructure large enough to swamp the margin. The shares must go
    // negative, not clamp to zero — a partner who owes us should be shown as
    // owing us.
    var c = InvoiceCalculator.Compute(
      InvoiceFixture.Input("2026-08") with { Infrastructure = 100_000m });

    using var _ = new AssertionScope();
    c.Result.NetProfit.Should().BeLessThan(0m);
    c.Result.Shares.Should().AllSatisfy(s => s.Earned.Should().BeLessThan(0m));
    c.Result.Shares.Sum(x => x.Earned).Should().Be(c.Result.MarketingSharePool);
  }
}
