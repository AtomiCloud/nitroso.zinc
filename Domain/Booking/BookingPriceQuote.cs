using CSharp_Result;
using Domain.Exceptions;

namespace Domain.Booking;

// The price accepted by the customer must be the exact price charged by this
// purchase request. Null keeps older API clients compatible; current clients
// always send the quote they displayed.
public static class BookingPriceQuote
{
  public static Result<decimal> Validate(decimal actual, decimal? expected)
  {
    if (expected == null || actual == expected.Value)
      return actual;

    return new BookingPriceChangedException(expected.Value, actual);
  }
}
