using App.Modules.Withdrawals.API.V1;
using Domain.Withdrawal;
using FluentAssertions;

namespace UnitTest.Withdrawals;

// The settings request's PayNowMode is a closed string enum; the booleans
// need no validation. Also pins the string <-> enum mapping both ways.
public class SetWithdrawalSettingsReqValidatorTests
{
  private readonly SetWithdrawalSettingsReqValidator validator = new();

  [Theory]
  [InlineData("Enabled")]
  [InlineData("Disabled")]
  [InlineData("FallbackOnly")]
  public void Known_modes_pass(string mode)
  {
    var result = validator.Validate(new SetWithdrawalSettingsReq(true, mode, false));
    result.IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData("")]
  [InlineData("enabled")]
  [InlineData("Fallback")]
  [InlineData("On")]
  public void Unknown_modes_are_rejected(string mode)
  {
    var result = validator.Validate(new SetWithdrawalSettingsReq(true, mode, false));
    result.IsValid.Should().BeFalse();
  }

  [Theory]
  [InlineData("Enabled", PayNowMode.Enabled)]
  [InlineData("Disabled", PayNowMode.Disabled)]
  [InlineData("FallbackOnly", PayNowMode.FallbackOnly)]
  public void Mode_maps_round_trip(string wire, PayNowMode domain)
  {
    wire.ToPayNowMode().Should().Be(domain);
    domain.ToRes().Should().Be(wire);
  }

  [Fact]
  public void Req_maps_to_record_and_record_maps_to_res()
  {
    var record = new SetWithdrawalSettingsReq(false, "FallbackOnly", true).ToDomain();
    record.Should()
      .Be(
        new WithdrawalSettingsRecord
        {
          CardRefundEnabled = false,
          PayNowMode = PayNowMode.FallbackOnly,
          SweepEnabled = true,
        }
      );

    record.ToRes().Should().Be(new WithdrawalSettingsRes(false, "FallbackOnly", true));
  }
}
