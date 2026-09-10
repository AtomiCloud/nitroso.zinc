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

// Bounds on the agreed terms. These are guard rails against a typo, not
// business rules — the numbers themselves are the partners' agreement.
public class SetInvoiceSettingsReqValidator : AbstractValidator<SetInvoiceSettingsReq>
{
  public SetInvoiceSettingsReqValidator()
  {
    // 100% would pay out every cent of profit; above it we would pay out more
    // than we made. Both are almost certainly a fat finger, so the ceiling is
    // the full pool.
    this.RuleFor(x => x.MarketingSharePct).InclusiveBetween(0m, 100m);

    // costs and recovery rates are never negative — a negative infrastructure
    // charge would silently inflate profit
    this.RuleFor(x => x.Infrastructure).GreaterThanOrEqualTo(0m);
    this.RuleFor(x => x.RecoveryPerBoost).GreaterThanOrEqualTo(0m);
    this.RuleFor(x => x.RecoveryPerTicket).GreaterThanOrEqualTo(0m);
  }
}

public class SetInvoicePartnerReqValidator : AbstractValidator<SetInvoicePartnerReq>
{
  public SetInvoicePartnerReqValidator()
  {
    // the suffix is the partner's identity across rows AND the letter on the
    // invoice reference (BB-2026-0801-C), so it has to be short and present
    this.RuleFor(x => x.Suffix).NotEmpty().MaximumLength(8);
    this.RuleFor(x => x.Name).NotEmpty().MaximumLength(128);

    this.RuleFor(x => x.RoundingPreference)
      .Must(x =>
        string.Equals(x, InvoiceMapper.RoundUp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(x, InvoiceMapper.RoundDown, StringComparison.OrdinalIgnoreCase)
      )
      .WithMessage(
        $"RoundingPreference must be '{InvoiceMapper.RoundUp}' or '{InvoiceMapper.RoundDown}'"
      );

    this.RuleFor(x => x.Position).GreaterThanOrEqualTo(0);
  }
}
