using System.Globalization;
using CSharp_Result;
using Domain.Exceptions;

namespace Domain.Booking;

// The price accepted by the customer must be the exact price charged by this
// purchase request. JavaScript transports the canonical token as text so no
// decimal precision is lost.
public static class BookingPriceQuote
{
  public static string Create(decimal cost) => cost.ToString("G29", CultureInfo.InvariantCulture);

  public static bool IsCanonical(string token) =>
    decimal.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var cost)
    && cost >= 0
    && Create(cost) == token;

  public static Result<decimal> Validate(decimal actual, string expected)
  {
    if (Create(actual) == expected)
      return actual;

    return new BookingPriceChangedException(expected, Create(actual));
  }
}
