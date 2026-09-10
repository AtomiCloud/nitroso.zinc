using App.Modules.Invoices.API.V1;
using Domain.Invoice;
using FluentAssertions;
using FluentAssertions.Execution;

namespace UnitTest.Invoices;

// The wire contract for POST Invoice/preview.
//
// The calculator is pinned to the cent by the golden and differential tests,
// but every one of those figures still has to survive two hand-written
// mappers to reach the browser. A transposed pair in ToRes() would produce a
// perfectly valid-looking invoice with the wrong money on it, and no
// calculator test would notice.
//
// So this drives the REAL June/July/August inputs through the full round trip
// — wire request -> domain -> compute -> wire response — and asserts the
// response carries the issued figures.
public class InvoicePreviewContractTests
{
  // ---- request mapping ----------------------------------------------------

  [Fact]
  public void The_request_maps_onto_the_domain_input_without_loss()
  {
    var domain = InvoiceFixture.Input("2026-08");
    var round = domain.ToReq().ToDomain();

    // Record equality is structural, so this compares every field at every
    // depth — arrays included. If a mapper ever drops one, this fails.
    round.Should().BeEquivalentTo(domain);
  }

  [Fact]
  public void A_null_recovery_stays_null_rather_than_becoming_zero()
  {
    // June has no recovery. Mapping null -> a zeroed record would be
    // invisible in the output (both compute to 0.00) until the day somebody
    // reads "recovery applies: false" off a month that had one.
    var june = InvoiceFixture.Input("2026-06");
    june.PartnerRecovery.Should().BeNull();
    june.ToReq().ToDomain().PartnerRecovery.Should().BeNull();
  }

  [Fact]
  public void The_overrides_survive_the_round_trip()
  {
    // If these were dropped on the wire, June would silently recompute to
    // different money than the document that was paid.
    var round = InvoiceFixture.Input("2026-06").ToReq().ToDomain();
    using var _ = new AssertionScope();
    round.FeeRateOverride.Should().Be(5.3347m);
    round.WastedFeeOverride.Should().Be(58.42m);
  }

  // ---- response mapping ---------------------------------------------------

  [Theory]
  [InlineData("2026-06")]
  [InlineData("2026-07")]
  [InlineData("2026-08")]
  public void The_response_carries_every_computed_figure(string month)
  {
    var computed = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    var res = computed.ToRes();

    using var _ = new AssertionScope();

    res.Fx.TotalFundedRm.Should().Be(computed.Fx.TotalFundedRm);
    res.Fx.TotalFundedSgd.Should().Be(computed.Fx.TotalFundedSgd);
    res.Fx.FxRate.Should().Be(computed.Fx.FxRate);
    res.Fx.FxRatePrinted.Should().Be(computed.Fx.FxRatePrinted);
    res.Fee.TotalPaymentFees.Should().Be(computed.Fee.TotalPaymentFees);
    res.Fee.FeeRatePct.Should().Be(computed.Fee.FeeRatePct);

    var routes = res.Routes.ToArray();
    routes.Should().HaveCount(computed.Routes.Length);
    foreach (var want in computed.Routes)
    {
      var got = routes.Single(x => x.Key == want.Key);
      got.Tickets.Should().Be(want.Tickets, "{0}", want.Key);
      got.Revenue.Should().Be(want.Revenue, "{0}", want.Key);
      got.FareRm.Should().Be(want.FareRm, "{0}", want.Key);
      got.TicketFaresRm.Should().Be(want.TicketFaresRm, "{0}", want.Key);
      got.TicketFaresSgd.Should().Be(want.TicketFaresSgd, "{0}", want.Key);
      got.ProcessingFee.Should().Be(want.ProcessingFee, "{0}", want.Key);
      got.DirectCost.Should().Be(want.DirectCost, "{0}", want.Key);
      got.Contribution.Should().Be(want.Contribution, "{0}", want.Key);
      got.MarginPct.Should().Be(want.MarginPct, "{0}", want.Key);
      got.TerminatedNet.Should().Be(want.TerminatedNet, "{0}", want.Key);
      got.SurchargeGross.Should().Be(want.SurchargeGross, "{0}", want.Key);
      got.DiscountGross.Should().Be(want.DiscountGross, "{0}", want.Key);
      got.Priority.Gross.Should().Be(want.Priority.Gross, "{0}", want.Key);
      got.Priority.FeeCost.Should().Be(want.Priority.FeeCost, "{0}", want.Key);
      got.Priority.Net.Should().Be(want.Priority.Net, "{0}", want.Key);
      got.PriceLines.Should().HaveCount(want.PriceLines.Length, "{0}", want.Key);
    }

    res.Totals.Tickets.Should().Be(computed.Totals.Tickets);
    res.Totals.Revenue.Should().Be(computed.Totals.Revenue);
    res.Totals.PricePerTicket.Should().Be(computed.Totals.PricePerTicket);
    res.Totals.Contribution.Should().Be(computed.Totals.Contribution);

    res.Adjustments.WastedFee.Should().Be(computed.Adjustments.WastedFee);
    res.Adjustments.WastedFeeComputed.Should().Be(computed.Adjustments.WastedFeeComputed);
    res.Adjustments.Infrastructure.Should().Be(computed.Adjustments.Infrastructure);

    res.Ancillary.Total.Should().Be(computed.Ancillary.Total);
    res.Ancillary.Priority.Net.Should().Be(computed.Ancillary.Priority.Net);
    res.Ancillary.Surcharge.CoveragePct.Should().Be(computed.Ancillary.Surcharge.CoveragePct);

    res.Recovery.Total.Should().Be(computed.Recovery.Total);
    res.Recovery.Applies.Should().Be(computed.Recovery.Applies);

    res.Result.NetProfit.Should().Be(computed.Result.NetProfit);
    res.Result.MarketingSharePool.Should().Be(computed.Result.MarketingSharePool);

    var shares = res.Result.Shares.ToArray();
    shares.Should().HaveCount(computed.Result.Shares.Length);
    foreach (var want in computed.Result.Shares)
    {
      var got = shares.Single(x => x.Suffix == want.Suffix);
      got.Name.Should().Be(want.Name, "{0}", want.Suffix);
      got.Earned.Should().Be(want.Earned, "{0}", want.Suffix);
      got.Advance.Should().Be(want.Advance, "{0}", want.Suffix);
      got.Amount.Should().Be(want.Amount, "{0}", want.Suffix);
    }
  }

  [Fact]
  public void The_full_round_trip_still_pays_what_the_issued_invoices_paid()
  {
    // The end-to-end statement: a request built from July's real figures,
    // through the endpoint's own mappers and calculator, pays exactly what
    // BunnyBooker actually transferred.
    var input = InvoiceFixture.Input("2026-07").ToReq().ToDomain();
    var res = InvoiceCalculator.Compute(input).ToRes();

    using var _ = new AssertionScope();
    res.Result.NetProfit.Should().Be(32023.46m);
    res.Result.Shares.Single(x => x.Suffix == "C").Amount.Should().Be(7767.36m);
    res.Result.Shares.Single(x => x.Suffix == "Z").Amount.Should().Be(7767.37m);
  }

  // ---- enums cross the wire as the strings the UI expects -----------------

  [Fact]
  public void Rounding_preference_crosses_the_wire_as_up_or_down()
  {
    var res = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-08")).ToRes();
    using var _ = new AssertionScope();
    res.Result.Shares.Single(x => x.Suffix == "C").RoundingPreference.Should().Be("down");
    res.Result.Shares.Single(x => x.Suffix == "Z").RoundingPreference.Should().Be("up");
  }

  [Fact]
  public void Price_line_kinds_cross_the_wire_as_policy_or_discount()
  {
    var res = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-08")).ToRes();
    var kinds = res.Routes.SelectMany(r => r.PriceLines).Select(l => l.Kind).Distinct();
    kinds.Should().OnlyContain(k => k == "policy" || k == "discount");
  }

  [Theory]
  [InlineData("UP", InvoiceRoundingPreference.Up)]
  [InlineData("up", InvoiceRoundingPreference.Up)]
  [InlineData("down", InvoiceRoundingPreference.Down)]
  // Anything unrecognised falls to Down, the conservative side: an unknown
  // preference must never silently hand somebody the leftover cent.
  [InlineData("sideways", InvoiceRoundingPreference.Down)]
  public void Rounding_preference_parses_case_insensitively(string wire, InvoiceRoundingPreference expected)
  {
    InvoiceMapper.ToRoundingPreference(wire).Should().Be(expected);
  }

  [Theory]
  [InlineData("discount", InvoicePriceLineKind.Discount)]
  [InlineData("DISCOUNT", InvoicePriceLineKind.Discount)]
  [InlineData("policy", InvoicePriceLineKind.Policy)]
  public void Price_line_kind_parses_case_insensitively(string wire, InvoicePriceLineKind expected)
  {
    InvoiceMapper.ToPriceLineKind(wire).Should().Be(expected);
  }

}
