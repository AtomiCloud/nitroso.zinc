using System.Globalization;
using CSharp_Result;
using Domain.Exceptions;

namespace Domain.Booking;

// The price accepted by the customer must be the exact price charged by this
// purchase request. JavaScript transports the canonical token as text so no
// decimal precision is lost.
public static class BookingPriceQuote
{
  // fixed-point, trailing zeros trimmed: "G29" would emit scientific notation
  // below 1e-4 ("1E-05" instead of "0.00001"). 28 fractional places is
  // decimal's full scale, so this stays lossless for every representable cost.
  private const string FixedPoint = "0.############################";

  public static string Create(decimal cost) =>
    cost.ToString(FixedPoint, CultureInfo.InvariantCulture);

  public static bool IsCanonical(string token) =>
    decimal.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var cost)
    && cost >= 0
    && Create(cost) == token;

  // A null expected quote is allowed for one release: old (raichu) argon
  // clients don't send it yet, so they are charged the server-computed price
  // unchecked. Tighten to required after raichu argon ships the quote flow.
  public static Result<decimal> Validate(decimal actual, string? expected)
  {
    if (expected == null || Create(actual) == expected)
      return actual;

    return new BookingPriceChangedException(expected, Create(actual));
  }
}
