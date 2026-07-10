using Domain.Booking;
using Domain.Exceptions;
using FluentAssertions;

namespace UnitTest.Bookings;

public class BookingPriceQuoteTests
{
  [Fact]
  public void Matching_expected_price_is_accepted()
  {
    BookingPriceQuote.Validate(15.12345678m, 15.12345678m).Get().Should().Be(15.12345678m);
  }

  [Fact]
  public void Missing_expected_price_keeps_older_clients_compatible()
  {
    BookingPriceQuote.Validate(15m, null).Get().Should().Be(15m);
  }

  [Fact]
  public void Any_price_change_is_rejected_even_below_one_cent()
  {
    var result = BookingPriceQuote.Validate(1.005m, 1.004m);

    result.IsSuccess().Should().BeFalse();
    var error = result.FailureOrDefault().Should().BeOfType<BookingPriceChangedException>().Subject;
    error.Expected.Should().Be(1.004m);
    error.Actual.Should().Be(1.005m);
  }
}
