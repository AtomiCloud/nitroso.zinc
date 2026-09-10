using System.Globalization;
using Domain.Invoice;

namespace App.Modules.Invoices.API.V1;

public static class InvoiceMapper
{
  // month-precision wire format; the day is always the 1st
  public const string MonthFormat = "MM-yyyy";

  public static InvoiceInputQuery ToDomain(this InvoiceInputQueryReq req) =>
    new()
    {
      Month = DateOnly.ParseExact(
        req.Month,
        MonthFormat,
        CultureInfo.InvariantCulture,
        DateTimeStyles.None
      ),
    };

  public static InvoiceInputRowRes ToRes(this InvoiceInputRow r) =>
    new(
      r.Month,
      r.GrossDeposits,
      new InvoiceInputFeesRes(r.Fees.Gateway, r.Fees.PaymentMethod),
      r.RefundFeesExcluded,
      r.Routes.Select(route => new InvoiceInputRouteRes(
        route.Key,
        (int)route.Direction,
        route.Tickets,
        route.Revenue,
        new InvoiceInputTerminatedRes(route.Terminated.Count, route.Terminated.KeptRevenue),
        new InvoiceInputPriorityRes(route.Priority.Paid, route.Priority.Fee, route.Priority.Free)
      )),
      new InvoiceInputWithdrawalsRes(
        r.Withdrawals.Count,
        r.Withdrawals.Total,
        r.Withdrawals.Income,
        r.Withdrawals.WithFee
      )
    );

  // ---- settings ----

  // Rounding preference travels as a string ("up"/"down") because that is
  // what the invoice's own JSON and the partner agreement call it; the
  // validator constrains it, so the fallback here is never reached in
  // practice and Down is the conservative side (a partner rounded down never
  // receives a cent that was not allocated to them).
  public const string RoundUp = "up";
  public const string RoundDown = "down";

  public static InvoiceRoundingPreference ToRoundingPreference(string s) =>
    string.Equals(s, RoundUp, StringComparison.OrdinalIgnoreCase)
      ? InvoiceRoundingPreference.Up
      : InvoiceRoundingPreference.Down;

  public static string ToWire(this InvoiceRoundingPreference p) =>
    p == InvoiceRoundingPreference.Up ? RoundUp : RoundDown;

  // Id/CreatedAt/EffectiveAt are assigned by the repository on insert; the
  // placeholders here are never read.
  public static InvoiceSettingsChange ToDomain(this SetInvoiceSettingsReq req) =>
    new()
    {
      Id = Guid.Empty,
      MarketingSharePct = req.MarketingSharePct,
      Infrastructure = req.Infrastructure,
      RecoveryPerBoost = req.RecoveryPerBoost,
      RecoveryPerTicket = req.RecoveryPerTicket,
      EffectiveAt = default,
      CreatedAt = default,
    };

  public static InvoicePartnerChange ToDomain(this SetInvoicePartnerReq req) =>
    new()
    {
      Id = Guid.Empty,
      Suffix = req.Suffix,
      Name = req.Name,
      RoundingPreference = ToRoundingPreference(req.RoundingPreference),
      Active = req.Active,
      Position = req.Position,
      EffectiveAt = default,
      CreatedAt = default,
    };

  public static InvoiceSettingsChangeRes ToRes(this InvoiceSettingsChange c) =>
    new(
      c.Id,
      c.MarketingSharePct,
      c.Infrastructure,
      c.RecoveryPerBoost,
      c.RecoveryPerTicket,
      c.EffectiveAt,
      c.CreatedAt
    );

  public static InvoicePartnerChangeRes ToRes(this InvoicePartnerChange c) =>
    new(
      c.Id,
      c.Suffix,
      c.Name,
      c.RoundingPreference.ToWire(),
      c.Active,
      c.Position,
      c.EffectiveAt,
      c.CreatedAt
    );

  public static InvoiceTermsRes ToRes(this InvoiceTerms t) =>
    new(
      t.MarketingSharePct,
      t.Infrastructure,
      t.RecoveryPerBoost,
      t.RecoveryPerTicket,
      t.Partners.Select(p => new InvoicePartnerRes(
        p.Suffix,
        p.Name,
        p.RoundingPreference.ToWire()
      ))
    );

  public static InvoiceSettingsRes ToRes(this InvoiceSettingsView v) =>
    new(
      v.Current?.ToRes(),
      v.Upcoming.Select(x => x.ToRes()),
      v.UpcomingPartners.Select(x => x.ToRes())
    );
}
