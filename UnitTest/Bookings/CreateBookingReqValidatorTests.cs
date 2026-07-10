using App.Modules.Bookings.API.V1;
using FluentAssertions;

namespace UnitTest.Bookings;

// ExpectedCost is optional for one release: old (raichu) argon clients don't
// send the quote yet. Null passes; when present it must be the canonical
// token Cost/summary returned. Tighten to required after raichu argon ships
// the quote flow.
public class CreateBookingReqValidatorTests
{
  private readonly CreateBookingReqValidator validator = new();

  private static CreateBookingReq Req(string? expectedCost) =>
    new(
      "10-08-2026",
      "08:30:00",
      "JToW",
      new BookingPassengerReq("Jane Doe", "F", "10-08-2030", "E1234567"),
      expectedCost
    );

  [Fact]
  public void Null_quote_passes_for_legacy_clients()
  {
    var result = validator.Validate(Req(null));
    result.IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData("15.12345678")]
  [InlineData("0.00001")]
  [InlineData("0")]
  public void Canonical_quote_passes(string quote)
  {
    var result = validator.Validate(Req(quote));
    result.IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData("")]
  [InlineData("01.00")]
  [InlineData("1.0")]
  [InlineData("-1")]
  [InlineData("1E-05")]
  [InlineData("not-a-price")]
  public void Present_but_noncanonical_quote_fails(string quote)
  {
    var result = validator.Validate(Req(quote));
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.PropertyName == "ExpectedCost");
  }
}
