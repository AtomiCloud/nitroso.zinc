using Domain.Invoice;
using FluentAssertions;

namespace UnitTest.Invoices;

// The rounding rule, pinned against JavaScript's Math.round — the oracle that
// produced the issued June, July and August invoices.
//
// The negative midpoint cases are the whole reason this file exists. Getting
// them wrong moves cents on real invoices and would not be caught by any
// positive-number test.
public class InvoiceRoundingTests
{
  [Theory]
  [InlineData(2.5, 3)]
  [InlineData(1.5, 2)]
  [InlineData(0.5, 1)]
  // half-up means toward POSITIVE INFINITY, so -2.5 rounds to -2, NOT -3.
  // C#'s MidpointRounding.AwayFromZero would give -3 here.
  [InlineData(-0.5, 0)]
  [InlineData(-1.5, -1)]
  [InlineData(-2.5, -2)]
  public void Midpoints_round_toward_positive_infinity(decimal input, decimal expected)
  {
    InvoiceRounding.R(input, 0).Should().Be(expected);
  }

  [Theory]
  [InlineData(-2.4, -2)]
  [InlineData(-2.6, -3)]
  [InlineData(2.4, 2)]
  [InlineData(2.6, 3)]
  public void Non_midpoints_round_to_the_nearer_value(decimal input, decimal expected)
  {
    InvoiceRounding.R(input, 0).Should().Be(expected);
  }

  [Theory]
  [InlineData(1.005, 1.01)]
  [InlineData(2.675, 2.68)]
  [InlineData(1.0049, 1.0)]
  public void Two_decimal_places_is_the_default(decimal input, decimal expected)
  {
    InvoiceRounding.R(input).Should().Be(expected);
  }

  [Fact]
  public void Negative_cents_round_toward_positive_infinity()
  {
    // discountGross is always negative and always passes through R at 2dp.
    // Both of these match the TS oracle exactly.
    InvoiceRounding.R(-266.995m).Should().Be(-266.99m);
    InvoiceRounding.R(-267.005m).Should().Be(-267.0m);
  }

  [Fact]
  public void The_printed_fx_rate_is_five_places()
  {
    // June: 8,985.64 / 28,100 = 0.319773665..., printed as 0.31977
    InvoiceRounding.R(8985.64m / 28100m, 5).Should().Be(0.31977m);
  }

  [Fact]
  public void The_computed_fee_rate_is_four_places()
  {
    // June: (720.50 + 1,172.42) / 35,483.50 x 100 = 5.3346485%, which rounds
    // to 5.3346 — NOT the 5.3347 the issued PDF printed and then applied.
    // That one-unit-in-the-last-place gap is exactly why 2026-06.json carries
    // feeRateOverride: 5.3347, and it is not a rounding bug on either side.
    InvoiceRounding.R((720.50m + 1172.42m) / 35483.50m * 100m, 4).Should().Be(5.3346m);
  }

  [Fact]
  public void Zero_and_exact_values_are_unchanged()
  {
    InvoiceRounding.R(0m).Should().Be(0m);
    InvoiceRounding.R(-0m).Should().Be(0m);
    InvoiceRounding.R(1234.56m).Should().Be(1234.56m);
    InvoiceRounding.R(1000m, 5).Should().Be(1000m);
  }

  [Fact]
  public void Six_places_is_used_by_the_share_allocation()
  {
    // baseCents floors r(rawShare * 100, 6)
    InvoiceRounding.R(841796.2499999m, 6).Should().Be(841796.25m);
  }

  // The one KNOWN divergence from the TS oracle, pinned so it is visible
  // rather than discovered later on an invoice.
  //
  // The oracle nudges by Number.EPSILON before rounding, intending to push
  // .5 away from zero. Whether that lands depends on float64 representation:
  // it stores -319.845 as -319.845000000000003, so the oracle answers
  // -319.85 (away from zero) — while for -266.995 the nudge is lost and it
  // answers -266.99 (toward positive infinity). Both cannot be a rule.
  //
  // Measured over 27,588 differential cases: the two engines agree
  // everywhere EXCEPT negative exact-decimal midpoints, where the oracle
  // itself is inconsistent (400 toward +inf vs 132 away from zero). We follow
  // what the TypeScript says it does. InvoiceCalculatorDifferentialTests is
  // the real gate — if this ever bites real invoice data, it fails there.
  [Fact]
  public void Negative_exact_midpoints_follow_the_documented_rule_not_the_float_noise()
  {
    InvoiceRounding.R(-319.845m).Should().Be(-319.84m, "toward positive infinity");
  }
}
