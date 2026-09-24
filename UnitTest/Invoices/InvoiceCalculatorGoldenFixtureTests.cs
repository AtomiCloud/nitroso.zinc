using Domain.Invoice;
using FluentAssertions;
using FluentAssertions.Execution;

namespace UnitTest.Invoices;

// The figures PRINTED ON THE ISSUED PDFs, asserted directly. Ported from
// invoices/verify.ts.
//
// This is the test that outlives the differential gate. The differential test
// proves C# matches the TypeScript; this one proves both match the documents
// that were actually sent to the partners and paid against. When invoices/ is
// deleted, this is what still says "June 2026 paid CLEON 4,135.58".
//
// Every number below is transcribed from a document, not computed here.
public class InvoiceCalculatorGoldenFixtureTests
{
  private static InvoiceComputed June => InvoiceCalculator.Compute(InvoiceFixture.Input("2026-06"));

  private static InvoiceComputed July => InvoiceCalculator.Compute(InvoiceFixture.Input("2026-07"));

  private static InvoiceComputed August => InvoiceCalculator.Compute(InvoiceFixture.Input("2026-08"));

  // ---- June 2026: BB-2026-0601-C / -Z -------------------------------------

  [Fact]
  public void June_reproduces_the_issued_fx_and_fee_rate()
  {
    var c = June;
    using var _ = new AssertionScope();
    c.Fx.TotalFundedRm.Should().Be(28100m);
    c.Fx.TotalFundedSgd.Should().Be(8985.64m);
    c.Fx.FxRatePrinted.Should().Be(0.31977m);
    c.Fee.TotalPaymentFees.Should().Be(1892.92m);
    // pinned by FeeRateOverride: the true rate is 5.3346485%, but the issued
    // PDF printed AND applied 5.3347%
    c.Fee.FeeRatePct.Should().Be(5.3347m);
  }

  [Fact]
  public void June_reproduces_the_issued_per_route_column()
  {
    var c = June;
    using var _ = new AssertionScope();

    var jbw = c.Routes.Single(x => x.Key == "jbw");
    jbw.TicketFaresRm.Should().Be(7810m);
    // the proof that FX is applied at FULL precision: 0.31977 flat gives 2497.40
    jbw.TicketFaresSgd.Should().Be(2497.43m);
    // the proof that the fee rate is applied at the PRINTED 4dp value
    jbw.ProcessingFee.Should().Be(827.25m);
    jbw.DirectCost.Should().Be(3324.68m);
    jbw.Contribution.Should().Be(12182.32m);
    jbw.MarginPct.Should().Be(78.6m);

    var wjb = c.Routes.Single(x => x.Key == "wjb");
    wjb.TicketFaresRm.Should().Be(20142.5m);
    wjb.TicketFaresSgd.Should().Be(6441.04m);
    wjb.ProcessingFee.Should().Be(609.54m);
    wjb.DirectCost.Should().Be(7050.58m);
    wjb.Contribution.Should().Be(4375.42m);
    wjb.MarginPct.Should().Be(38.3m);
  }

  [Fact]
  public void June_reproduces_the_issued_totals()
  {
    var c = June;
    using var _ = new AssertionScope();
    c.Totals.Tickets.Should().Be(2713);
    c.Totals.Revenue.Should().Be(26933m);
    c.Totals.PricePerTicket.Should().Be(9.93m);
    c.Totals.TicketFaresRm.Should().Be(27952.5m);
    // 2,497.43 + 6,441.04 — the sum of the ROUNDED route figures
    c.Totals.TicketFaresSgd.Should().Be(8938.47m);
    c.Totals.ProcessingFee.Should().Be(1436.79m);
    c.Totals.DirectCost.Should().Be(10375.26m);
    c.Totals.Contribution.Should().Be(16557.74m);
  }

  [Fact]
  public void June_reproduces_the_issued_adjustments()
  {
    var c = June;
    using var _ = new AssertionScope();
    c.Adjustments.TerminatedNet.Should().Be(543.02m);
    c.Adjustments.TerminatedCount.Should().Be(174);
    c.Adjustments.Infrastructure.Should().Be(500m);
    // The one irreducible discrepancy. The PDF prints 58.42; the formula gives
    // 1,095 x 5.3347% = 58.4150, which rounds to 58.41. No rate derivable from
    // the printed inputs yields 58.42 (the true underlying rate must have been
    // ~5.33471%), so the issued document is honoured via WastedFeeOverride and
    // the one-cent variance stays visible in WastedFeeComputed rather than
    // being silently absorbed.
    c.Adjustments.WastedFee.Should().Be(58.42m);
    c.Adjustments.WastedFeeComputed.Should().Be(58.41m);
  }

  [Fact]
  public void June_reproduces_the_issued_payout()
  {
    var c = June;
    using var _ = new AssertionScope();
    c.Result.NetProfit.Should().Be(16542.34m);
    c.Result.NetMarginPct.Should().Be(61.4m);
    c.Result.MarketingSharePool.Should().Be(8271.17m);

    // The half-cent. 8,271.17 / 2 = 4,135.585, and the leftover cent goes to
    // ZOEY because her rounding preference is "up" — exactly as the issued
    // PDFs resolved it.
    c.Result.Shares.Single(x => x.Suffix == "C").Amount.Should().Be(4135.58m);
    c.Result.Shares.Single(x => x.Suffix == "Z").Amount.Should().Be(4135.59m);

    // June predates the out-of-system recovery, so nothing is withheld.
    c.Recovery.Applies.Should().BeFalse();
    c.Result.Shares.Should().AllSatisfy(s => s.Advance.Should().Be(0m));
  }

  // ---- July 2026 -----------------------------------------------------------

  [Fact]
  public void July_reproduces_the_issued_payout()
  {
    var c = July;
    using var _ = new AssertionScope();
    c.Totals.Tickets.Should().Be(4777);
    c.Totals.Revenue.Should().Be(47765m);
    c.Totals.PricePerTicket.Should().Be(10m);
    c.Result.NetProfit.Should().Be(32023.46m);
    c.Result.MarketingSharePool.Should().Be(16011.73m);

    // 159 partner tickets x $3, no free boosts (waived for July and August)
    c.Recovery.Tickets.Should().Be(159);
    c.Recovery.FreeBoosts.Should().Be(0);
    c.Recovery.Total.Should().Be(477m);

    c.Result.Shares.Single(x => x.Suffix == "C").Amount.Should().Be(7767.36m);
    c.Result.Shares.Single(x => x.Suffix == "Z").Amount.Should().Be(7767.37m);
  }

  // ---- August 2026 ---------------------------------------------------------

  [Fact]
  public void August_reproduces_the_issued_payout()
  {
    var c = August;
    using var _ = new AssertionScope();
    c.Totals.Tickets.Should().Be(5265);
    c.Totals.Revenue.Should().Be(53219m);
    c.Totals.PricePerTicket.Should().Be(10.11m);
    c.Result.NetProfit.Should().Be(37471.51m);
    c.Result.MarketingSharePool.Should().Be(18735.76m);

    c.Recovery.Tickets.Should().Be(139);
    c.Recovery.Total.Should().Be(417m);

    c.Result.Shares.Single(x => x.Suffix == "C").Amount.Should().Be(9159.38m);
    c.Result.Shares.Single(x => x.Suffix == "Z").Amount.Should().Be(9159.38m);
  }

  [Fact]
  public void August_splits_the_pool_without_losing_a_cent()
  {
    // The case that forced largest-remainder allocation: the pool is
    // 18,735.76 and half of it is 9,367.88 exactly, but the SHARE BASE half
    // (37,471.51 / 4 = 9,367.8775) rounds the same way for both partners.
    // Rounding each share independently loses a cent against the pool.
    var c = August;
    var sum = c.Result.Shares.Sum(x => x.Earned);
    sum.Should().Be(c.Result.MarketingSharePool);
  }
}
