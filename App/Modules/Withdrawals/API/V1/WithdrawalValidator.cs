using App.Utility;
using FluentValidation;

namespace App.Modules.Withdrawals.API.V1;

public class SearchWithdrawalQueryValidator : AbstractValidator<SearchWithdrawalQuery>
{
  public SearchWithdrawalQueryValidator()
  {
    this.RuleFor(x => x.Min)
      .GreaterThanOrEqualTo(0);
    this.RuleFor(x => x.Max)
      .LessThanOrEqualTo(0);


    this.RuleFor(x => x.Before)
      .NullableDateValid();
    this.RuleFor(x => x.After)
      .NullableDateValid();

    this.RuleFor(x => x.Limit)
      .Limit();
    this.RuleFor(x => x.Skip)
      .Skip();
  }
}

public class CreateWithdrawalReqValidator : AbstractValidator<CreateWithdrawalReq>
{
  public CreateWithdrawalReqValidator()
  {
    this.RuleFor(x => x.Amount)
      .GreaterThan(0);
    this.RuleFor(x => x.PayNowNumber)
      .NotEmpty()
      .MaximumLength(64);
  }
}

public class CancelWithdrawalReqValidator : AbstractValidator<CancelWithdrawalReq>
{
  public CancelWithdrawalReqValidator()
  {
    this.RuleFor(x => x.Note)
      .NotEmpty()
      .MaximumLength(4096);
  }
}


public class RejectWithdrawalReqValidator : AbstractValidator<RejectWithdrawalReq>
{
  public RejectWithdrawalReqValidator()
  {
    this.RuleFor(x => x.Note)
      .NotEmpty()
      .MaximumLength(4096);
  }
}
