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
