using System.Globalization;
using FluentValidation;

namespace App.Modules.Invoices.API.V1;

public class InvoiceInputQueryReqValidator : AbstractValidator<InvoiceInputQueryReq>
{
  public InvoiceInputQueryReqValidator()
  {
    this.RuleFor(x => x.Month)
      .NotEmpty()
      .Must(x =>
        DateOnly.TryParseExact(
          x,
          InvoiceMapper.MonthFormat,
          CultureInfo.InvariantCulture,
          DateTimeStyles.None,
          out _
        )
      )
      .WithMessage($"Month must be in the format of {InvoiceMapper.MonthFormat}");
  }
}
