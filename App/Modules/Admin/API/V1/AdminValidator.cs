using App.Utility;
using FluentValidation;

namespace App.Modules.Admin.API.V1;

public class TransferReqValidator : AbstractValidator<TransferReq>
{
  public TransferReqValidator()
  {
    this.RuleFor(x => x.Amount).NotNull().Must(x => x > 0);
    this.RuleFor(x => x.Desc).NotNull().TransactionDescriptionValid();
  }
}
