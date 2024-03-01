using App.Utility;
using FluentValidation;

namespace App.Modules.Payments.API.V1;

public class SearchPaymentQueryValidator : AbstractValidator<SearchPaymentQuery>
{
  public SearchPaymentQueryValidator()
  {
    this.RuleFor(x => x.Min)
      .GreaterThanOrEqualTo(0);

    this.RuleFor(x => x.Max)
      .GreaterThan(0);

    this.RuleFor(x => x.CreatedBefore)
      .NullableDateValid();
    this.RuleFor(x => x.CreatedAfter)
      .NullableDateValid();
    this.RuleFor(x => x.LastUpdatedBefore)
      .NullableDateValid();
    this.RuleFor(x => x.LastUpdatedAfter)
      .NullableDateValid();
  }
}

public class CreatePaymentReqValidator : AbstractValidator<CreatePaymentReq>
{
  public CreatePaymentReqValidator()
  {
    this.RuleFor(x => x.Currency)
      .NotNull()
      .ValidEnum(["SGD"]);

    this.RuleFor(x => x.Amount)
      .NotNull()
      .GreaterThanOrEqualTo(5)
      .PrecisionScale(5, 2, true);
  }
}
