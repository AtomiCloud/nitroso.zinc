using FluentValidation;

namespace App.Modules.Fees.API.V1;

public class AddFeeReqValidator : AbstractValidator<AddFeeReq>
{
  public AddFeeReqValidator()
  {
    this.RuleFor(x => x.Percentage).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    this.RuleFor(x => x.FlatAmount).GreaterThanOrEqualTo(0).LessThanOrEqualTo(10_000);
    // a past effective date would insert a row that can never win the
    // newest-effective ordering — a silently dead change (small tolerance
    // for clock skew; omit the field for an immediate change)
    this.RuleFor(x => x.EffectiveAt)
      .Must(x => x == null || x > DateTime.UtcNow.AddMinutes(-5))
      .WithMessage("EffectiveAt must be in the future (omit it for an immediate change)");
  }
}
