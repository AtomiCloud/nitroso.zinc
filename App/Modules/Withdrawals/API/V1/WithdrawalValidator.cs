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
  private static readonly string[] Methods = ["PayNow", "CardRefund"];

  public CreateWithdrawalReqValidator()
  {
    this.RuleFor(x => x.Amount).GreaterThan(0);

    // absent = PayNow (rollout compat for already-deployed frontends)
    this.RuleFor(x => x.Method)
      .Must(m => string.IsNullOrEmpty(m) || Methods.Contains(m))
      .WithMessage("Method must be 'PayNow' or 'CardRefund'");

    // PayNow needs a destination account: exactly 8 digits, an SG PayNow
    // mobile number, matching the UI rule and the +65 normalization the
    // payout gateway applies. CardRefund has no PayNow id — the money
    // returns to the cards that funded the wallet.
    this.When(
      x => x.Method != "CardRefund",
      () => this.RuleFor(x => x.PayNowNumber).NotEmpty().Matches("^[0-9]{8}$")
    );
    this.When(
      x => x.Method == "CardRefund",
      () =>
        this.RuleFor(x => x.PayNowNumber)
          .Empty()
          .WithMessage("A card-refund withdrawal must not carry a PayNow number")
    );
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
