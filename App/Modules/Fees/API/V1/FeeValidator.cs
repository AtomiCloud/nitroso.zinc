using FluentValidation;

namespace App.Modules.Fees.API.V1;

public class AddFeeReqValidator : AbstractValidator<AddFeeReq>
{
  public AddFeeReqValidator()
  {
    this.RuleFor(x => x.Percentage).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    this.RuleFor(x => x.FlatAmount)
      .GreaterThanOrEqualTo(0)
      .LessThanOrEqualTo(10_000)
      .Must(WholeCents)
      .WithMessage("FlatAmount must not have sub-cent precision");
    // a past effective date would insert a row that can never win the
    // newest-effective ordering — a silently dead change (small tolerance
    // for clock skew; omit the field for an immediate change)
    this.RuleFor(x => x.EffectiveAt)
      .Must(x => x == null || x > DateTime.UtcNow.AddMinutes(-5))
      .WithMessage("EffectiveAt must be in the future (omit it for an immediate change)");
    // null = uncapped; a zero cap would silently make every fee free.
    // Whole cents only: the fee rounds to cents BEFORE clamping, so a
    // sub-cent cap would settle sub-cent amounts that disagree with the
    // ledger text
    this.RuleFor(x => x.Cap)
      .GreaterThan(0)
      .LessThanOrEqualTo(100_000)
      .Must(c => c == null || WholeCents(c.Value))
      .When(x => x.Cap != null)
      .WithMessage(
        "Cap must be greater than 0, at most 100000, and in whole cents (omit it for no cap)"
      );
  }

  private static bool WholeCents(decimal value) => decimal.Round(value, 2) == value;
}
