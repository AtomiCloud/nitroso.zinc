using App.Utility;
using FluentValidation;

namespace App.Modules.Costs.API.V1;

public class CreateCostReqValidator : AbstractValidator<CreateCostReq>
{
  public CreateCostReqValidator()
  {
    this.RuleFor(x => x.Cost)
      .GreaterThanOrEqualTo(0)
      .WithMessage("Cost has to be larger than or equal to 0");
  }
}

public class CostPolicyReqValidator : AbstractValidator<CostPolicyReq>
{
  public CostPolicyReqValidator()
  {
    this.RuleFor(x => x.Name).NotNull().NameValid();
    this.RuleFor(x => x.MatchDate).NullableDateValid();
    this.RuleFor(x => x.MatchTime).NullableTimeValid();
    // Enum.TryParse would happily accept numerics ("9") — names only
    this.RuleFor(x => x.MatchDayOfWeek)
      .Must(x => x is null || Enum.GetNames<DayOfWeek>().Contains(x))
      .WithMessage("MatchDayOfWeek must be one of: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday");
    this.RuleFor(x => x.MatchDirection)!.TrainDirectionValid();
    this.RuleFor(x => x.LeadTimeUnderHours)
      .GreaterThanOrEqualTo(1)
      .LessThanOrEqualTo(8760)
      .When(x => x.LeadTimeUnderHours != null)
      .WithMessage("LeadTimeUnderHours must be between 1 and 8760 (a year)");
    // Amount is deliberately SIGNED: negative = discount
    this.RuleFor(x => x.Amount)
      .GreaterThanOrEqualTo(-100)
      .LessThanOrEqualTo(100)
      .When(x => x.IsPercentage)
      .WithMessage("A percentage Amount must be between -100 and 100");
    this.RuleFor(x => x.Amount)
      .GreaterThanOrEqualTo(-10_000)
      .LessThanOrEqualTo(10_000)
      .When(x => !x.IsPercentage)
      .WithMessage("A flat Amount must be between -10000 and 10000");
    this.RuleFor(x => x.ExpiresAt)
      .Must((req, x) => x == null || req.EffectiveAt == null || x > req.EffectiveAt)
      .WithMessage("ExpiresAt must be after EffectiveAt");
  }
}

public class CostSummaryQueryValidator : AbstractValidator<CostSummaryQuery>
{
  public CostSummaryQueryValidator()
  {
    this.RuleFor(x => x.Date).NotNull().DateValid();
    this.RuleFor(x => x.Time).NotNull().TimeValid();
    this.RuleFor(x => x.Direction).NotNull().TrainDirectionValid();
  }
}
