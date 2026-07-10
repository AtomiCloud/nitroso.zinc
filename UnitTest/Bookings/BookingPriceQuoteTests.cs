using System.Globalization;
using Domain.Booking;
using Domain.Exceptions;
using FluentAssertions;

namespace UnitTest.Bookings;

public class BookingPriceQuoteTests
{
  [Fact]
  public void Matching_expected_price_is_accepted()
  {
    var quote = BookingPriceQuote.Create(15.12345678m);

    quote.Should().Be("15.12345678");
    BookingPriceQuote.Validate(15.12345678m, quote).Get().Should().Be(15.12345678m);
  }

  [Fact]
  public void Any_price_change_is_rejected_even_below_one_cent()
  {
    var result = BookingPriceQuote.Validate(1.005m, BookingPriceQuote.Create(1.004m));

    result.IsSuccess().Should().BeFalse();
    var error = result.FailureOrDefault().Should().BeOfType<BookingPriceChangedException>().Subject;
    error.Expected.Should().Be("1.004");
    error.Actual.Should().Be("1.005");
  }

  [Fact]
  public void Quote_preserves_decimal_precision_that_json_numbers_cannot()
  {
    const decimal final = 13.2563635034720316m;

    BookingPriceQuote.Create(final).Should().Be("13.2563635034720316");
    BookingPriceQuote.Validate(final, "13.2563635034720316").IsSuccess().Should().BeTrue();
    BookingPriceQuote.IsCanonical("13.2563635034720316").Should().BeTrue();
    BookingPriceQuote.IsCanonical("13.256363503472032").Should().BeTrue();
    BookingPriceQuote.Validate(final, "13.256363503472032").IsSuccess().Should().BeFalse();
  }

  [Theory]
  [InlineData("01.00")]
  [InlineData("1.0")]
  [InlineData("-1")]
  [InlineData("not-a-price")]
  [InlineData("1E-05")] // scientific notation is not the canonical form
  public void Noncanonical_quote_tokens_are_rejected(string token)
  {
    BookingPriceQuote.IsCanonical(token).Should().BeFalse();
  }

  // "G29" would render these as "1E-05"/"1E-08"; the canonical token must be
  // plain fixed-point so JavaScript, the validator regex-free canonical check
  // and the customer all see the same digits
  [Theory]
  [InlineData("0.00001")]
  [InlineData("0.00000001")]
  public void Tiny_quotes_are_fixed_point_not_scientific(string token)
  {
    var cost = decimal.Parse(token, CultureInfo.InvariantCulture);

    BookingPriceQuote.Create(cost).Should().Be(token);
    BookingPriceQuote.IsCanonical(token).Should().BeTrue();
    BookingPriceQuote.Validate(cost, token).IsSuccess().Should().BeTrue();
  }

  // Optional for one release: old (raichu) argon clients don't send the
  // quote — a null quote skips validation and charges the server price
  [Fact]
  public void Null_quote_is_accepted_at_the_server_price()
  {
    BookingPriceQuote.Validate(15.12345678m, null).Get().Should().Be(15.12345678m);
  }
}
