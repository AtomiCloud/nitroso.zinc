using App.Utility;
using FluentValidation;

namespace App.Modules.Discounts.API.V1;

public class DiscountSearchQueryValidator : AbstractValidator<DiscountSearchQuery>
{
  public DiscountSearchQueryValidator()
  {
    this.RuleFor(x => x.Search)
      .Length(1, 256)
      .WithMessage("Name has to be between 1 to 256 characters");
    this.RuleFor(x => x.DiscountType).DiscountTypeValid();
    this.RuleFor(x => x.MatchMode).DiscountMatchModeValid();
    this.RuleForEach(x => x.MatchTarget).MaximumLength(255);
  }
}

public class DiscountMatchReqValidator : AbstractValidator<DiscountMatchReq>
{
  public DiscountMatchReqValidator()
  {
    this.RuleFor(x => x.Value)
      .NotNull()
      .Length(1, 256)
      .WithMessage("Value has to be between 1 to 256 characters");
    this.RuleFor(x => x.MatchType).NotNull().DiscountMatchTypeValid();
  }
}

public class DiscountTargetReqValidator : AbstractValidator<DiscountTargetReq>
{
  public DiscountTargetReqValidator()
  {
    this.RuleFor(x => x.MatchMode).NotNull().DiscountMatchModeValid();
    this.RuleForEach(x => x.Matches).NotNull().SetValidator(new DiscountMatchReqValidator());
  }
}

public class DiscountRecordReqValidator : AbstractValidator<DiscountRecordReq>
{
  public DiscountRecordReqValidator()
  {
    this.RuleFor(x => x.Name)
      .NotNull()
      .Length(1, 256)
      .WithMessage("Name has to be between 1 to 256 characters");
    this.RuleFor(x => x.Description)
      .NotNull()
      .Length(1, 2048)
      .WithMessage("Description has to be between 1 to 2048 characters");
    this.RuleFor(x => x.Amount).NotNull().GreaterThanOrEqualTo(0);
    this.RuleFor(x => x.Type).NotNull().DiscountTypeValid();
  }
}

public class DiscountStatusReqValidator : AbstractValidator<DiscountStatusReq>
{
  public DiscountStatusReqValidator()
  {
    this.RuleFor(x => x.Disabled).NotNull();
  }
}

public class CreateDiscountReqValidator : AbstractValidator<CreateDiscountReq>
{
  public CreateDiscountReqValidator()
  {
    this.RuleFor(x => x.Target).NotNull().SetValidator(new DiscountTargetReqValidator());
    this.RuleFor(x => x.Record).NotNull().SetValidator(new DiscountRecordReqValidator());
  }
}

public class UpdateDiscountReqValidator : AbstractValidator<UpdateDiscountReq>
{
  public UpdateDiscountReqValidator()
  {
    this.RuleFor(x => x.Target).NotNull().SetValidator(new DiscountTargetReqValidator());
    this.RuleFor(x => x.Record).NotNull().SetValidator(new DiscountRecordReqValidator());
    this.RuleFor(x => x.Status).NotNull().SetValidator(new DiscountStatusReqValidator());
  }
}
