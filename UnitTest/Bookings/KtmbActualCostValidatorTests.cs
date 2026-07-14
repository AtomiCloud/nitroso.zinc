using App.Modules.Bookings.API.V1;
using FluentAssertions;

namespace UnitTest.Bookings;

// The request validation around actual-KTMB-cost capture: the paired
// optional form fields on complete/{id}, the required backfill body, and the
// MYR -> SGD FX rate queue (rate bounds + no-past effective dating, the
// SetKtmbCostReq convention).
public class KtmbActualCostValidatorTests
{
  // ---- complete/{id} optional form fields ----

  private readonly CompleteKtmbCostReqValidator completeValidator = new();

  [Fact]
  public void Complete_without_ktmb_fields_is_valid()
  {
    completeValidator.Validate(new CompleteKtmbCostReq(null, null)).IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData("MYR")]
  [InlineData("SGD")]
  public void Complete_with_both_fields_is_valid(string currency)
  {
    completeValidator
      .Validate(new CompleteKtmbCostReq(35.5m, currency))
      .IsValid.Should()
      .BeTrue();
  }

  [Fact]
  public void Complete_with_amount_but_no_currency_is_rejected()
  {
    completeValidator.Validate(new CompleteKtmbCostReq(35.5m, null)).IsValid.Should().BeFalse();
  }

  [Fact]
  public void Complete_with_currency_but_no_amount_is_rejected()
  {
    completeValidator.Validate(new CompleteKtmbCostReq(null, "MYR")).IsValid.Should().BeFalse();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void Complete_with_non_positive_amount_is_rejected(decimal amount)
  {
    completeValidator
      .Validate(new CompleteKtmbCostReq(amount, "MYR"))
      .IsValid.Should()
      .BeFalse();
  }

  [Theory]
  [InlineData("USD")]
  [InlineData("myr")]
  [InlineData("")]
  public void Complete_with_unknown_currency_is_rejected(string currency)
  {
    completeValidator
      .Validate(new CompleteKtmbCostReq(35.5m, currency))
      .IsValid.Should()
      .BeFalse();
  }

  // ---- the backfill body ----

  private readonly SetBookingKtmbCostReqValidator backfillValidator = new();

  [Theory]
  [InlineData("MYR")]
  [InlineData("SGD")]
  public void Backfill_with_positive_amount_and_known_currency_is_valid(string currency)
  {
    backfillValidator
      .Validate(new SetBookingKtmbCostReq(35.5m, currency))
      .IsValid.Should()
      .BeTrue();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void Backfill_with_non_positive_amount_is_rejected(decimal amount)
  {
    backfillValidator
      .Validate(new SetBookingKtmbCostReq(amount, "MYR"))
      .IsValid.Should()
      .BeFalse();
  }

  [Theory]
  [InlineData("USD")]
  [InlineData("sgd")]
  public void Backfill_with_unknown_currency_is_rejected(string currency)
  {
    backfillValidator
      .Validate(new SetBookingKtmbCostReq(35.5m, currency))
      .IsValid.Should()
      .BeFalse();
  }

  // ---- the FX rate queue ----

  private readonly SetKtmbFxRateReqValidator fxValidator = new();

  [Theory]
  [InlineData(0.01)]
  [InlineData(0.32)]
  [InlineData(100)]
  public void Fx_rate_within_bounds_is_valid(decimal rate)
  {
    fxValidator.Validate(new SetKtmbFxRateReq(rate, null)).IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-0.3)]
  [InlineData(100.01)]
  public void Fx_rate_out_of_bounds_is_rejected(decimal rate)
  {
    fxValidator.Validate(new SetKtmbFxRateReq(rate, null)).IsValid.Should().BeFalse();
  }

  [Fact]
  public void Fx_future_effective_date_is_valid()
  {
    fxValidator
      .Validate(new SetKtmbFxRateReq(0.32m, DateTime.UtcNow.AddDays(1)))
      .IsValid.Should()
      .BeTrue();
  }

  [Fact]
  public void Fx_past_effective_date_is_rejected()
  {
    // a past effective date inserts a row that can never win the
    // newest-effective ordering — a silently dead change
    fxValidator
      .Validate(new SetKtmbFxRateReq(0.32m, DateTime.UtcNow.AddDays(-1)))
      .IsValid.Should()
      .BeFalse();
  }

  [Fact]
  public void Fx_omitted_effective_date_means_immediate_and_is_valid()
  {
    fxValidator.Validate(new SetKtmbFxRateReq(0.32m, null)).IsValid.Should().BeTrue();
  }
}
