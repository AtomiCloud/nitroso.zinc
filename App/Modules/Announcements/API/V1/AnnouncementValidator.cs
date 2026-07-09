using Domain;
using FluentValidation;

namespace App.Modules.Announcements.API.V1;

public class SendFeeAnnouncementReqValidator : AbstractValidator<SendFeeAnnouncementReq>
{
  public SendFeeAnnouncementReqValidator()
  {
    this.RuleFor(x => x.Type)
      .Must(x => Enum.TryParse<FeeType>(x, true, out _))
      .WithMessage("Type must be 'Withdrawal' or 'Deposit'");
    this.RuleFor(x => x.Reasoning).MaximumLength(4096);
  }
}
