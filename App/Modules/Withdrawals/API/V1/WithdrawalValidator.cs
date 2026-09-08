using App.Utility;
using Domain.Withdrawal;
using FluentValidation;
using System.Linq.Expressions;

namespace App.Modules.Withdrawals.API.V1;

public class SearchWithdrawalQueryValidator : AbstractValidator<SearchWithdrawalQuery>
{
  public SearchWithdrawalQueryValidator()
  {
    this.CommonWithdrawalFilterRules(
      x => x.Min,
      x => x.Max,
      x => x.Before,
      x => x.After,
      x => x.Status
    );

    this.RuleFor(x => x.Limit).Limit();
    this.RuleFor(x => x.Skip).Skip();
  }
}

public class ExportWithdrawalQueryValidator : AbstractValidator<ExportWithdrawalQuery>
{
  public ExportWithdrawalQueryValidator()
  {
    this.CommonWithdrawalFilterRules(
      x => x.Min,
      x => x.Max,
      x => x.Before,
      x => x.After,
      x => x.Status
    );
  }
}

file static class WithdrawalQueryRules
{
  private static readonly string[] Statuses = Enum.GetNames<WithdrawStatus>();

  public static void CommonWithdrawalFilterRules<T>(
    this AbstractValidator<T> validator,
    Expression<Func<T, decimal?>> min,
    Expression<Func<T, decimal?>> max,
    Expression<Func<T, string?>> before,
    Expression<Func<T, string?>> after,
    Expression<Func<T, string?>> status
  )
  {
    validator.RuleFor(min).GreaterThanOrEqualTo(0);
    validator.RuleFor(max).GreaterThanOrEqualTo(0);
    validator.RuleFor(before).NullableDateValid();
    validator.RuleFor(after).NullableDateValid();
    validator
      .RuleFor(status)
      .Must(value => value is null || Statuses.Contains(value))
      .WithMessage("Status must be one of: " + string.Join(", ", Statuses));
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

public class ReconcileRefundsQueryValidator : AbstractValidator<ReconcileRefundsQuery>
{
  public ReconcileRefundsQueryValidator()
  {
    this.RuleFor(x => x.After).NullableDateValid();
    this.RuleFor(x => x.Before).NullableDateValid();
  }
}

public class SetWithdrawalSettingsReqValidator : AbstractValidator<SetWithdrawalSettingsReq>
{
  private static readonly string[] Modes = ["Enabled", "Disabled", "FallbackOnly"];

  public SetWithdrawalSettingsReqValidator()
  {
    this.RuleFor(x => x.PayNowMode)
      .Must(m => Modes.Contains(m))
      .WithMessage("PayNowMode must be 'Enabled', 'Disabled' or 'FallbackOnly'");
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
