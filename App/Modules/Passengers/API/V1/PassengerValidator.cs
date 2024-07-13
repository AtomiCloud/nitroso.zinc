using App.Utility;
using FluentValidation;

namespace App.Modules.Passengers.API.V1;

public class CreatePassengerReqValidator : AbstractValidator<CreatePassengerReq>
{
  public CreatePassengerReqValidator()
  {
    this.RuleFor(x => x.FullName)
      .NotNull()
      .MaximumLength(512)
      .Matches("^[a-zA-Z @./',\\-`*]+$");
    this.RuleFor(x => x.PassportNumber)
      .NotNull()
      .MaximumLength(20)
      .Matches("^([a-zA-Z0-9]+)$");
    this.RuleFor(x => x.PassportExpiry)
      .NotNull()
      .DateValid();
    this.RuleFor(x => x.Gender)
      .NotNull()
      .GenderValid();
  }
}

public class UpdatePassengerReqValidator : AbstractValidator<UpdatePassengerReq>
{
  public UpdatePassengerReqValidator()
  {
    this.RuleFor(x => x.FullName)
      .NotNull()
      .MaximumLength(512)
      .Matches("^[a-zA-Z @./',\\-`*]+$");
    this.RuleFor(x => x.PassportNumber)
      .NotNull()
      .MaximumLength(20)
      .Matches("^([a-zA-Z0-9]+)$");
    this.RuleFor(x => x.PassportExpiry)
      .NotNull()
      .DateValid();
    this.RuleFor(x => x.Gender)
      .NotNull()
      .GenderValid();
  }
}

public class PassengerSearchQueryValidator : AbstractValidator<SearchPassengerQuery>
{
  public PassengerSearchQueryValidator()
  {
    this.RuleFor(x => x.Limit)
      .Limit();
    this.RuleFor(x => x.Skip)
      .Skip();
  }
}
