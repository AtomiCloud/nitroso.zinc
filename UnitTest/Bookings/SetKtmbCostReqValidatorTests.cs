using App.Modules.Bookings.API.V1;
using FluentAssertions;

namespace UnitTest.Bookings;

// POST Booking/ktmb-cost: the owner enters the KTMB fare after the month it
// applied to, so a PAST effective date is a normal request, not a mistake.
public class SetKtmbCostReqValidatorTests
{
  private readonly SetKtmbCostReqValidator validator = new();

  [Fact]
  public void Immediate_change_is_valid()
  {
    validator.Validate(new SetKtmbCostReq("JToW", 16.15m, null)).IsValid.Should().BeTrue();
  }

  [Fact]
  public void Backdated_change_is_valid()
  {
    var sep1Sgt = new DateTime(2026, 8, 31, 16, 0, 0, DateTimeKind.Utc);
    validator.Validate(new SetKtmbCostReq("WToJ", 16.15m, sep1Sgt)).IsValid.Should().BeTrue();
  }

  [Fact]
  public void Future_change_is_valid()
  {
    validator
      .Validate(new SetKtmbCostReq("JToW", 17m, DateTime.UtcNow.AddDays(30)))
      .IsValid.Should()
      .BeTrue();
  }

  [Theory]
  [InlineData(-0.01)]
  [InlineData(10000.01)]
  public void Cost_out_of_range_is_rejected(double cost)
  {
    validator
      .Validate(new SetKtmbCostReq("JToW", (decimal)cost, null))
      .IsValid.Should()
      .BeFalse();
  }

  [Fact]
  public void Unknown_direction_is_rejected()
  {
    validator.Validate(new SetKtmbCostReq("Sideways", 10m, null)).IsValid.Should().BeFalse();
  }
}
