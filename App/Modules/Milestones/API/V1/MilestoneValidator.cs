using App.Utility;
using FluentValidation;

namespace App.Modules.Milestones.API.V1;

public class CreateMilestoneReqValidator : AbstractValidator<CreateMilestoneReq>
{
  public CreateMilestoneReqValidator()
  {
    this.RuleFor(x => x.Date).NotNull().DateValid();
    this.RuleFor(x => x.Label).NotEmpty().MaximumLength(256);
  }
}
