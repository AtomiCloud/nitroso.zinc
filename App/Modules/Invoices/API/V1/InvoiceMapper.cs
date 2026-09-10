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

  // ---- preview ----

  public const string LinePolicy = "policy";
  public const string LineDiscount = "discount";

  public static InvoicePriceLineKind ToPriceLineKind(string s) =>
    string.Equals(s, LineDiscount, StringComparison.OrdinalIgnoreCase)
      ? InvoicePriceLineKind.Discount
      : InvoicePriceLineKind.Policy;

  public static string ToWire(this InvoicePriceLineKind k) =>
    k == InvoicePriceLineKind.Discount ? LineDiscount : LinePolicy;

  public static InvoiceMonthInput ToDomain(this PreviewInvoiceReq r) =>
    new()
    {
      Period = new InvoicePeriod
      {
        Label = r.Period.Label,
        MonthName = r.Period.MonthName,
        Seq = r.Period.Seq,
      },
      IssueDate = r.IssueDate,
      DueDate = r.DueDate,
      Topups = r.Topups.Select(t => new InvoiceTopup
        {
          Date = t.Date,
          Rm = t.Rm,
          Sgd = t.Sgd,
        })
        .ToArray(),
      TopupNote = r.TopupNote ?? string.Empty,
      Fees = new InvoiceFeesInput { Gateway = r.Fees.Gateway, PaymentMethod = r.Fees.PaymentMethod },
      GrossDeposits = r.GrossDeposits,
      RefundFeesExcluded = r.RefundFeesExcluded,
      Routes = r.Routes.Select(rt => new InvoiceRouteInput
        {
          Key = rt.Key,
          Label = rt.Label,
          Short = rt.Short,
          Tickets = rt.Tickets,
          Revenue = rt.Revenue,
          FareRm = rt.FareRm,
          Terminated = new InvoiceTerminatedInput
          {
            Count = rt.Terminated.Count,
            KeptRevenue = rt.Terminated.KeptRevenue,
            HalfFareSgd = rt.Terminated.HalfFareSgd,
          },
        })
        .ToArray(),
      Withdrawals = new InvoiceWithdrawalsInput
      {
        Count = r.Withdrawals.Count,
        Total = r.Withdrawals.Total,
      },
      Infrastructure = r.Infrastructure,
      MarketingSharePct = r.MarketingSharePct,
      Partners = r.Partners.Select(p => new InvoicePartner
        {
          Suffix = p.Suffix,
          Name = p.Name,
          RoundingPreference = ToRoundingPreference(p.RoundingPreference),
        })
        .ToArray(),
      Priority = new InvoicePriorityInput
      {
        PerRoute = r.Priority.PerRoute.ToDictionary(
          kv => kv.Key,
          kv => new InvoicePriorityRouteInput
          {
            Paid = kv.Value.Paid,
            Fee = kv.Value.Fee,
            Free = kv.Value.Free,
          }
        ),
        KeptOnCancelled = r.Priority.KeptOnCancelled,
        KeptOnCancelledCount = r.Priority.KeptOnCancelledCount,
      },
      Surcharge = new InvoiceSurchargeInput
      {
        Coverage = new InvoiceSurchargeCoverage
        {
          WithBreakdown = r.Surcharge.Coverage.WithBreakdown,
          Total = r.Surcharge.Coverage.Total,
        },
        PerRoute = r.Surcharge.PerRoute.ToDictionary(
          kv => kv.Key,
          kv => kv.Value.Select(l => new InvoicePriceLine
            {
              Kind = ToPriceLineKind(l.Kind),
              Name = l.Name,
              Count = l.Count,
              Delta = l.Delta,
            })
            .ToArray()
        ),
      },
      WithdrawalFee = new InvoiceWithdrawalFeeInput
      {
        Income = r.WithdrawalFee.Income,
        WithFee = r.WithdrawalFee.WithFee,
        Count = r.WithdrawalFee.Count,
      },
      Promotional = new InvoicePromotionalInput
      {
        Count = r.Promotional.Count,
        Amount = r.Promotional.Amount,
      },
      NetTransfers = r.NetTransfers,
      Duplicates = new InvoiceDuplicatesInput
      {
        Count = r.Duplicates.Count,
        Refunded = r.Duplicates.Refunded,
      },
      PartnerRecovery = r.PartnerRecovery is null
        ? null
        : new InvoiceRecoveryInput
        {
          FreeBoosts = r.PartnerRecovery.FreeBoosts,
          Tickets = r.PartnerRecovery.Tickets,
          PerBoost = r.PartnerRecovery.PerBoost,
          PerTicket = r.PartnerRecovery.PerTicket,
        },
      FeeRateOverride = r.FeeRateOverride,
      WastedFeeOverride = r.WastedFeeOverride,
    };

  public static InvoiceComputedRes ToRes(this InvoiceComputed c) =>
    new(
      new InvoiceFxRes(
        c.Fx.TotalFundedRm,
        c.Fx.TotalFundedSgd,
        c.Fx.FxRate,
        c.Fx.FxRatePrinted
      ),
      new InvoiceFeeRes(c.Fee.TotalPaymentFees, c.Fee.FeeRatePct),
      c.Routes.Select(rt => new InvoiceComputedRouteRes(
        rt.Key,
        rt.Label,
        rt.Short,
        rt.Tickets,
        rt.Revenue,
        rt.FareRm,
        new PreviewTerminatedReq(
          rt.Terminated.Count,
          rt.Terminated.KeptRevenue,
          rt.Terminated.HalfFareSgd
        ),
        rt.TicketFaresRm,
        rt.TicketFaresSgd,
        rt.ProcessingFee,
        rt.DirectCost,
        rt.Contribution,
        rt.MarginPct,
        rt.TerminatedNet,
        new InvoicePriorityRouteRes(
          rt.Priority.Paid,
          rt.Priority.Fee,
          rt.Priority.Free,
          rt.Priority.Gross,
          rt.Priority.FeeCost,
          rt.Priority.Net
        ),
        rt.PriceLines.Select(l => new InvoicePriceLineRes(
          l.Kind.ToWire(),
          l.Name,
          l.Count,
          l.Delta
        )),
        rt.SurchargeGross,
        rt.DiscountGross
      )),
      new InvoiceTotalsRes(
        c.Totals.Tickets,
        c.Totals.Revenue,
        c.Totals.PricePerTicket,
        c.Totals.TicketFaresRm,
        c.Totals.TicketFaresSgd,
        c.Totals.ProcessingFee,
        c.Totals.DirectCost,
        c.Totals.Contribution,
        c.Totals.TotalMarginPct
      ),
      new InvoiceAdjustmentsRes(
        c.Adjustments.TerminatedNet,
        c.Adjustments.TerminatedCount,
        c.Adjustments.WastedFee,
        c.Adjustments.WastedFeeComputed,
        c.Adjustments.Infrastructure
      ),
      new InvoiceAncillaryRes(
        new InvoiceAncillaryPriorityRes(
          c.Ancillary.Priority.Gross,
          c.Ancillary.Priority.FeeCost,
          c.Ancillary.Priority.Net,
          c.Ancillary.Priority.Paid,
          c.Ancillary.Priority.Free,
          c.Ancillary.Priority.Kept,
          c.Ancillary.Priority.KeptCount
        ),
        new InvoiceAncillarySurchargeRes(
          c.Ancillary.Surcharge.Gross,
          c.Ancillary.Surcharge.Discounts,
          new PreviewCoverageReq(
            c.Ancillary.Surcharge.Coverage.WithBreakdown,
            c.Ancillary.Surcharge.Coverage.Total
          ),
          c.Ancillary.Surcharge.CoveragePct
        ),
        new PreviewWithdrawalFeeReq(
          c.Ancillary.WithdrawalFee.Income,
          c.Ancillary.WithdrawalFee.WithFee,
          c.Ancillary.WithdrawalFee.Count
        ),
        c.Ancillary.Promotional,
        c.Ancillary.NetTransfers,
        new PreviewDuplicatesReq(c.Ancillary.Duplicates.Count, c.Ancillary.Duplicates.Refunded),
        c.Ancillary.Total
      ),
      new InvoiceRecoveryRes(
        c.Recovery.FreeBoosts,
        c.Recovery.Tickets,
        c.Recovery.PerBoost,
        c.Recovery.PerTicket,
        c.Recovery.Boosts,
        c.Recovery.TicketsAmount,
        c.Recovery.Total,
        c.Recovery.Applies
      ),
      new InvoiceResultRes(
        c.Result.NetProfit,
        c.Result.NetMarginPct,
        c.Result.ShareBase,
        c.Result.MarketingSharePool,
        c.Result.Shares.Select(s => new InvoiceShareRes(
          s.Name,
          s.Suffix,
          s.RoundingPreference.ToWire(),
          s.Pct,
          s.Earned,
          s.Advance,
          s.Amount
        ))
      )
    );
}
