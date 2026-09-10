namespace Domain.Invoice;

// The invoice's rounding rule, ported from `r()` in invoices/calc.ts.
//
// This is its own file because it is the most dangerous line in the port. The
// TypeScript engine is the oracle that produced the issued June, July and
// August invoices, and it rounds like this:
//
//     Math.round((n + Number.EPSILON * Math.sign(n)) * 10**dp) / 10**dp
//
// Math.round is half-up toward POSITIVE INFINITY (Math.round(-2.5) === -2),
// which is already not C#'s default — MidpointRounding.AwayFromZero gives -3.
// Negatives really do flow through here: discountGross is always negative,
// netTransfers was -55.00 in June, and terminatedNet can be. So the C#
// default would silently move cents on real invoices.
//
// MEASURED DIVERGENCE (27,588 differential cases against the live TS engine).
// The epsilon nudge is NOT a no-op. Its intent is to push .5 away from zero,
// and it succeeds or fails depending on whether n * 10**dp happens to be
// representable in float64:
//
//     r(-0.005)   -> -0.01   (nudge landed; rounds away from zero)
//     r(-319.845) -> -319.85 (float64 stores it as -319.845000000000003)
//     r(-266.995) -> -266.99 (nudge lost; rounds toward positive infinity)
//
// Across the differential set, at negative exact-decimal midpoints the oracle
// agreed with toward-positive-infinity 400 times and with away-from-zero 132
// times. There is no rule that reproduces both — the oracle's answer at those
// points is float representation noise, not a decision anyone made. Every
// single divergence is at a negative exact midpoint; away from those, the two
// engines agree on all 27,588 cases.
//
// So this rounds toward positive infinity, matching what the TypeScript
// SAYS it does (Math.round) and what it does 3 times in 4 when the float
// disagrees. The real gate is InvoiceCalculatorDifferentialTests: the C#
// engine must reproduce the issued June, July and August invoices to the
// cent. If a divergence ever lands on real invoice data, that test fails
// loudly rather than the difference being absorbed.
public static class InvoiceRounding
{
  // half-up toward positive infinity, matching JavaScript Math.round
  public static decimal R(decimal n, int dp = 2)
  {
    var factor = Pow10(dp);
    return decimal.Floor(n * factor + 0.5m) / factor;
  }

  private static decimal Pow10(int dp)
  {
    var f = 1m;
    for (var i = 0; i < dp; i++)
      f *= 10m;
    return f;
  }
}
