using App.Utility;
using FluentValidation;

namespace App.Modules.Withdrawals.API.V1;

public class SearchWithdrawalQueryValidator : AbstractValidator<SearchWithdrawalQuery>
{
  public SearchWithdrawalQueryValidator()
  {
    this.RuleFor(x => x.Min).GreaterThanOrEqualTo(0);
    this.RuleFor(x => x.Max).LessThanOrEqualTo(0);

    this.RuleFor(x => x.Before).NullableDateValid();
    this.RuleFor(x => x.After).NullableDateValid();

    this.RuleFor(x => x.Limit).Limit();
    this.RuleFor(x => x.Skip).Skip();
  }
}

public class CreateWithdrawalReqValidator : AbstractValidator<CreateWithdrawalReq>
{
  public CreateWithdrawalReqValidator()
  {
    this.RuleFor(x => x.Amount).GreaterThan(0);
    // exactly 8 digits: an SG PayNow mobile number, matching the UI rule and
    // the +65 normalization the payout gateway applies
    this.RuleFor(x => x.PayNowNumber).NotEmpty().Matches("^[0-9]{8}$");
  }
}

public class SetFeeReqValidator : AbstractValidator<SetFeeReq>
{
  public SetFeeReqValidator()
  {
    this.RuleFor(x => x.WithdrawFeePercentage).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    // a past effective date would insert a row that can never win the
    // newest-effective ordering — a silently dead change (small tolerance
    // for clock skew; omit the field for an immediate change)
    this.RuleFor(x => x.EffectiveAt)
      .Must(x => x == null || x > DateTime.UtcNow.AddMinutes(-5))
      .WithMessage("EffectiveAt must be in the future (omit it for an immediate change)");
  }
}

public class CancelWithdrawalReqValidator : AbstractValidator<CancelWithdrawalReq>
{
  public CancelWithdrawalReqValidator()
  {
    this.RuleFor(x => x.Note).NotEmpty().MaximumLength(4096);
  }
}

public class RejectWithdrawalReqValidator : AbstractValidator<RejectWithdrawalReq>
{
  public RejectWithdrawalReqValidator()
  {
    this.RuleFor(x => x.Note).NotEmpty().MaximumLength(4096);
  }
}
