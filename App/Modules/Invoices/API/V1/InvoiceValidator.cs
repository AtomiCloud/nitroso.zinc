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

// Structural guard rails on a previewed month. Deliberately NOT a
// plausibility check on the money: the operator is allowed to preview a
// loss-making month, a month with a huge correction, or a deliberately odd
// what-if. What is rejected is a payload the engine cannot make sense of —
// a route with no key, a partner with no suffix, a share above 100%.
public class PreviewInvoiceReqValidator : AbstractValidator<PreviewInvoiceReq>
{
  public PreviewInvoiceReqValidator()
  {
    this.RuleFor(x => x.Period).NotNull();
    this.RuleFor(x => x.Fees).NotNull();
    this.RuleFor(x => x.Withdrawals).NotNull();
    this.RuleFor(x => x.Priority).NotNull();
    this.RuleFor(x => x.Surcharge).NotNull();
    this.RuleFor(x => x.WithdrawalFee).NotNull();
    this.RuleFor(x => x.Promotional).NotNull();
    this.RuleFor(x => x.Duplicates).NotNull();

    this.RuleFor(x => x.MarketingSharePct).InclusiveBetween(0m, 100m);

    // An invoice with no route prints nothing and divides by zero ticket
    // count. The engine guards it, but a caller that sent this meant something
    // else.
    this.RuleFor(x => x.Routes).NotEmpty();
    this.RuleForEach(x => x.Routes)
      .ChildRules(rt =>
      {
        rt.RuleFor(x => x.Key).NotEmpty();
        // Negative tickets are not a what-if, they are a bug upstream.
        rt.RuleFor(x => x.Tickets).GreaterThanOrEqualTo(0);
        rt.RuleFor(x => x.FareRm).GreaterThanOrEqualTo(0m);
        rt.RuleFor(x => x.Terminated).NotNull();
      });

    // No partners means nobody to pay. The engine returns an empty share list
    // rather than throwing, but as a request it is meaningless.
    this.RuleFor(x => x.Partners).NotEmpty();
    this.RuleForEach(x => x.Partners)
      .ChildRules(p =>
      {
        p.RuleFor(x => x.Suffix).NotEmpty().MaximumLength(8);
        p.RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        p.RuleFor(x => x.RoundingPreference)
          .Must(x =>
            string.Equals(x, InvoiceMapper.RoundUp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x, InvoiceMapper.RoundDown, StringComparison.OrdinalIgnoreCase)
          )
          .WithMessage(
            $"RoundingPreference must be '{InvoiceMapper.RoundUp}' or '{InvoiceMapper.RoundDown}'"
          );
      });

    // Two partners cannot share one suffix — the allocation keys on it and the
    // invoice reference letter would collide.
    this.RuleFor(x => x.Partners)
      .Must(ps =>
        ps.Select(p => p.Suffix)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .Count() == ps.Length
      )
      .WithMessage("Partner suffixes must be unique")
      .When(x => x.Partners is { Length: > 0 });

    // Coverage below 100% means the surcharge figures are understated, which
    // is reported. Coverage ABOVE 100% is impossible and would print a
    // nonsense percentage.
    // Every rule below reaches INTO Surcharge, so each is gated on it being
    // present. FluentValidation does not stop at a failed NotNull — without
    // the gate a null payload dereferences instead of returning 400.
    this.RuleFor(x => x.Surcharge.Coverage.WithBreakdown)
      .GreaterThanOrEqualTo(0)
      .When(x => x.Surcharge?.Coverage is not null);
    this.RuleFor(x => x.Surcharge.Coverage.Total)
      .GreaterThanOrEqualTo(0)
      .When(x => x.Surcharge?.Coverage is not null);
    this.RuleFor(x => x.Surcharge)
      .Must(s => s.Coverage.WithBreakdown <= s.Coverage.Total)
      .WithMessage("Surcharge coverage cannot exceed the total")
      .When(x => x.Surcharge?.Coverage is not null);

    // Written as a Must over the whole dictionary rather than RuleForEach over
    // `PerRoute.Values.SelectMany(...)`: FluentValidation infers the reported
    // property name from the expression, and a SelectMany projection is not a
    // property, so that form throws InvalidOperationException at validation
    // time — for EVERY request, valid or not.
    this.RuleFor(x => x.Surcharge)
      .Must(s =>
        s.PerRoute.Values.SelectMany(v => v ?? []).All(l => IsKnownPriceLineKind(l.Kind))
      )
      .WithMessage(
        $"Price line kind must be '{InvoiceMapper.LinePolicy}' or '{InvoiceMapper.LineDiscount}'"
      )
      .When(x => x.Surcharge?.PerRoute is not null);

    // The recovery is an advance against the partner's share. A negative rate
    // would pay them MORE for money they collected outside the system.
    this.RuleFor(x => x.PartnerRecovery!.FreeBoosts)
      .GreaterThanOrEqualTo(0)
      .When(x => x.PartnerRecovery is not null);
    this.RuleFor(x => x.PartnerRecovery!.Tickets)
      .GreaterThanOrEqualTo(0)
      .When(x => x.PartnerRecovery is not null);
    this.RuleFor(x => x.PartnerRecovery!.PerBoost)
      .GreaterThanOrEqualTo(0m)
      .When(x => x.PartnerRecovery is not null);
    this.RuleFor(x => x.PartnerRecovery!.PerTicket)
      .GreaterThanOrEqualTo(0m)
      .When(x => x.PartnerRecovery is not null);

    // A pinned rate only exists to reproduce an issued document; outside 0-100
    // it is a typo.
    this.RuleFor(x => x.FeeRateOverride!.Value)
      .InclusiveBetween(0m, 100m)
      .When(x => x.FeeRateOverride is not null);
  }

  private static bool IsKnownPriceLineKind(string kind) =>
    string.Equals(kind, InvoiceMapper.LinePolicy, StringComparison.OrdinalIgnoreCase)
    || string.Equals(kind, InvoiceMapper.LineDiscount, StringComparison.OrdinalIgnoreCase);
}
