using Domain.Invoice;
using FluentAssertions;
using FluentAssertions.Execution;

namespace UnitTest.Invoices;

// THE GATE for the calc.ts -> C# port.
//
// Fixtures/oracle-YYYY-MM.json is the TypeScript engine's own output for the
// same input file, dumped from invoices/calc.ts. Every figure the C# engine
// produces must equal it EXACTLY — decimal equality, no tolerance. A tolerance
// here would defeat the point: the float/decimal divergence this port risks is
// measured in cents, and a cent per month across two partners is precisely the
// kind of error that is never noticed and never forgiven.
//
// June carries FeeRateOverride 5.3347 and WastedFeeOverride 58.42, so this also
// pins that a pinned invoice still reproduces its issued figures.
//
// MIGRATION-ONLY. Once the port is trusted and invoices/ is retired, this file
// goes with it — the golden-fixture and invariant tests are what stay. Until
// then the TypeScript is the oracle and this is how we prove we match it.
public class InvoiceCalculatorDifferentialTests
{
  public static TheoryData<string> Months => new() { "2026-06", "2026-07", "2026-08" };

  [Theory]
  [MemberData(nameof(Months))]
  public void The_csharp_engine_reproduces_the_typescript_engine_exactly(string month)
  {
    var actual = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    var oracle = InvoiceFixture.Oracle(month);

    using var _ = new AssertionScope();

    // ---- FX ---------------------------------------------------------------
    var fx = oracle.Fx!;
    actual.Fx.TotalFundedRm.Should().Be(fx.TotalFundedRm);
    actual.Fx.TotalFundedSgd.Should().Be(fx.TotalFundedSgd);
    actual.Fx.FxRatePrinted.Should().Be(fx.FxRatePrinted);
    // FxRate is carried at full precision and decimal holds far more digits
    // than float64, so the two cannot be bit-equal. What matters is that it
    // agrees to well beyond any figure it feeds — every downstream use is
    // rounded to the cent. 10 places is ~1e-6 cents on a 45,000 RM top-up.
    actual.Fx.FxRate.Should().BeApproximately(fx.FxRate, 0.0000000001m);

    // ---- fee rate ---------------------------------------------------------
    actual.Fee.TotalPaymentFees.Should().Be(oracle.Fee!.TotalPaymentFees);
    actual.Fee.FeeRatePct.Should().Be(oracle.Fee.FeeRatePct);

    // ---- per route --------------------------------------------------------
    actual.Routes.Should().HaveCount(oracle.Routes.Length);
    foreach (var want in oracle.Routes)
    {
      var got = actual.Routes.Single(x => x.Key == want.Key);
      got.TicketFaresRm.Should().Be(want.TicketFaresRm, "{0} ticketFaresRm", want.Key);
      got.TicketFaresSgd.Should().Be(want.TicketFaresSgd, "{0} ticketFaresSgd", want.Key);
      got.ProcessingFee.Should().Be(want.ProcessingFee, "{0} processingFee", want.Key);
      got.DirectCost.Should().Be(want.DirectCost, "{0} directCost", want.Key);
      got.Contribution.Should().Be(want.Contribution, "{0} contribution", want.Key);
      got.MarginPct.Should().Be(want.MarginPct, "{0} marginPct", want.Key);
      got.TerminatedNet.Should().Be(want.TerminatedNet, "{0} terminatedNet", want.Key);
      got.SurchargeGross.Should().Be(want.SurchargeGross, "{0} surchargeGross", want.Key);
      got.DiscountGross.Should().Be(want.DiscountGross, "{0} discountGross", want.Key);

      var p = want.Priority!;
      got.Priority.Paid.Should().Be(p.Paid, "{0} priority.paid", want.Key);
      got.Priority.Fee.Should().Be(p.Fee, "{0} priority.fee", want.Key);
      got.Priority.Free.Should().Be(p.Free, "{0} priority.free", want.Key);
      got.Priority.Gross.Should().Be(p.Gross, "{0} priority.gross", want.Key);
      got.Priority.FeeCost.Should().Be(p.FeeCost, "{0} priority.feeCost", want.Key);
      got.Priority.Net.Should().Be(p.Net, "{0} priority.net", want.Key);
    }

    // ---- totals -----------------------------------------------------------
    var t = oracle.Totals!;
    actual.Totals.Tickets.Should().Be(t.Tickets);
    actual.Totals.Revenue.Should().Be(t.Revenue);
    actual.Totals.PricePerTicket.Should().Be(t.PricePerTicket);
    actual.Totals.TicketFaresRm.Should().Be(t.TicketFaresRm);
    actual.Totals.TicketFaresSgd.Should().Be(t.TicketFaresSgd);
    actual.Totals.ProcessingFee.Should().Be(t.ProcessingFee);
    actual.Totals.DirectCost.Should().Be(t.DirectCost);
    actual.Totals.Contribution.Should().Be(t.Contribution);
    actual.Totals.TotalMarginPct.Should().Be(t.TotalMarginPct);

    // ---- adjustments ------------------------------------------------------
    var adj = oracle.Adjustments!;
    actual.Adjustments.TerminatedNet.Should().Be(adj.TerminatedNet);
    actual.Adjustments.TerminatedCount.Should().Be(adj.TerminatedCount);
    actual.Adjustments.WastedFee.Should().Be(adj.WastedFee);
    actual.Adjustments.WastedFeeComputed.Should().Be(adj.WastedFeeComputed);
    actual.Adjustments.Infrastructure.Should().Be(adj.Infrastructure);

    // ---- ancillary --------------------------------------------------------
    var anc = oracle.Ancillary!;
    var ap = anc.Priority!;
    actual.Ancillary.Priority.Gross.Should().Be(ap.Gross);
    actual.Ancillary.Priority.FeeCost.Should().Be(ap.FeeCost);
    actual.Ancillary.Priority.Net.Should().Be(ap.Net);
    actual.Ancillary.Priority.Paid.Should().Be(ap.Paid);
    actual.Ancillary.Priority.Free.Should().Be(ap.Free);
    actual.Ancillary.Priority.Kept.Should().Be(ap.Kept);
    actual.Ancillary.Priority.KeptCount.Should().Be(ap.KeptCount);

    var asur = anc.Surcharge!;
    actual.Ancillary.Surcharge.Gross.Should().Be(asur.Gross);
    actual.Ancillary.Surcharge.Discounts.Should().Be(asur.Discounts);
    actual.Ancillary.Surcharge.CoveragePct.Should().Be(asur.CoveragePct);

    actual.Ancillary.WithdrawalFee.Income.Should().Be(anc.WithdrawalFee!.Income);
    actual.Ancillary.WithdrawalFee.WithFee.Should().Be(anc.WithdrawalFee.WithFee);
    actual.Ancillary.WithdrawalFee.Count.Should().Be(anc.WithdrawalFee.Count);
    actual.Ancillary.Promotional.Should().Be(anc.Promotional);
    actual.Ancillary.NetTransfers.Should().Be(anc.NetTransfers);
    actual.Ancillary.Total.Should().Be(anc.Total);

    // ---- recovery ---------------------------------------------------------
    var rec = oracle.Recovery!;
    actual.Recovery.FreeBoosts.Should().Be(rec.FreeBoosts);
    actual.Recovery.Tickets.Should().Be(rec.Tickets);
    actual.Recovery.PerBoost.Should().Be(rec.PerBoost);
    actual.Recovery.PerTicket.Should().Be(rec.PerTicket);
    actual.Recovery.Boosts.Should().Be(rec.Boosts);
    actual.Recovery.TicketsAmount.Should().Be(rec.TicketsAmount);
    actual.Recovery.Total.Should().Be(rec.Total);
    actual.Recovery.Applies.Should().Be(rec.Applies);

    // ---- the money --------------------------------------------------------
    var res = oracle.Result!;
    actual.Result.NetProfit.Should().Be(res.NetProfit);
    actual.Result.NetMarginPct.Should().Be(res.NetMarginPct);
    actual.Result.ShareBase.Should().Be(res.ShareBase);
    actual.Result.MarketingSharePool.Should().Be(res.MarketingSharePool);

    actual.Result.Shares.Should().HaveCount(res.Shares.Length);
    foreach (var want in res.Shares)
    {
      var got = actual.Result.Shares.Single(x => x.Suffix == want.Suffix);
      got.Name.Should().Be(want.Name);
      got.Pct.Should().Be(want.Pct, "{0} pct", want.Suffix);
      got.Earned.Should().Be(want.Earned, "{0} earned", want.Suffix);
      got.Advance.Should().Be(want.Advance, "{0} advance", want.Suffix);
      got.Amount.Should().Be(want.Amount, "{0} amount", want.Suffix);
    }
  }
}
