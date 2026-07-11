using App.Modules.Withdrawals.API.V1;
using FluentAssertions;

namespace UnitTest.Withdrawals;

// PayNowNumber is conditionally required: mandatory (8 digits) for PayNow —
// including requests that omit Method entirely (rollout compat) — and
// forbidden for CardRefund.
public class CreateWithdrawalReqValidatorTests
{
  private readonly CreateWithdrawalReqValidator validator = new();

  [Fact]
  public void Paynow_with_valid_number_passes()
  {
    var result = validator.Validate(new CreateWithdrawalReq(100m, "91234567", "PayNow"));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void Omitted_method_defaults_to_paynow_rules()
  {
    validator
      .Validate(new CreateWithdrawalReq(100m, "91234567", null))
      .IsValid.Should()
      .BeTrue();
    validator
      .Validate(new CreateWithdrawalReq(100m, null, null))
      .IsValid.Should()
      .BeFalse("legacy requests still need a PayNow number");
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void Card_refund_without_paynow_number_passes(string? payNow)
  {
    var result = validator.Validate(new CreateWithdrawalReq(100m, payNow, "CardRefund"));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void Card_refund_with_paynow_number_is_rejected()
  {
    var result = validator.Validate(new CreateWithdrawalReq(100m, "91234567", "CardRefund"));
    result.IsValid.Should().BeFalse();
  }

  [Theory]
  [InlineData("1234567")]
  [InlineData("123456789")]
  [InlineData("abcdefgh")]
  public void Paynow_number_must_be_8_digits(string payNow)
  {
    var result = validator.Validate(new CreateWithdrawalReq(100m, payNow, "PayNow"));
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void Unknown_method_is_rejected()
  {
    var result = validator.Validate(new CreateWithdrawalReq(100m, null, "Cheque"));
    result.IsValid.Should().BeFalse();
  }

  [Fact]
  public void Non_positive_amount_is_rejected()
  {
    validator.Validate(new CreateWithdrawalReq(0m, "91234567", "PayNow")).IsValid.Should().BeFalse();
    validator
      .Validate(new CreateWithdrawalReq(-5m, "91234567", "PayNow"))
      .IsValid.Should()
      .BeFalse();
  }
}
