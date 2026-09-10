using System.Globalization;
using App.Modules.Invoices.Data;
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

  // Domain -> wire request. The inverse of the ToDomain above, needed because
  // a stored invoice hands its frozen inputs back to the browser in exactly
  // the shape the browser would send them — so "open this invoice, change one
  // figure, preview again" is a round trip through the same type rather than a
  // second hand-written shape that could drift from the first.
  public static PreviewInvoiceReq ToReq(this InvoiceMonthInput m) =>
    new(
      new PreviewPeriodReq(m.Period.Label, m.Period.MonthName, m.Period.Seq),
      m.IssueDate,
      m.DueDate,
      m.Topups.Select(t => new PreviewTopupReq(t.Date, t.Rm, t.Sgd)).ToArray(),
      m.TopupNote,
      new PreviewFeesReq(m.Fees.Gateway, m.Fees.PaymentMethod),
      m.GrossDeposits,
      m.RefundFeesExcluded,
      m.Routes.Select(rt => new PreviewRouteReq(
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
        )
      )).ToArray(),
      new PreviewWithdrawalsReq(m.Withdrawals.Count, m.Withdrawals.Total),
      m.Infrastructure,
      m.MarketingSharePct,
      m.Partners
        .Select(p => new PreviewPartnerReq(p.Suffix, p.Name, p.RoundingPreference.ToWire()))
        .ToArray(),
      new PreviewPriorityReq(
        m.Priority.PerRoute.ToDictionary(
          kv => kv.Key,
          kv => new PreviewPriorityRouteReq(kv.Value.Paid, kv.Value.Fee, kv.Value.Free)
        ),
        m.Priority.KeptOnCancelled,
        m.Priority.KeptOnCancelledCount
      ),
      new PreviewSurchargeReq(
        new PreviewCoverageReq(m.Surcharge.Coverage.WithBreakdown, m.Surcharge.Coverage.Total),
        m.Surcharge.PerRoute.ToDictionary(
          kv => kv.Key,
          kv => kv.Value
            .Select(l => new PreviewPriceLineReq(l.Kind.ToWire(), l.Name, l.Count, l.Delta))
            .ToArray()
        )
      ),
      new PreviewWithdrawalFeeReq(
        m.WithdrawalFee.Income,
        m.WithdrawalFee.WithFee,
        m.WithdrawalFee.Count
      ),
      new PreviewPromotionalReq(m.Promotional.Count, m.Promotional.Amount),
      m.NetTransfers,
      new PreviewDuplicatesReq(m.Duplicates.Count, m.Duplicates.Refunded),
      m.PartnerRecovery is null
        ? null
        : new PreviewRecoveryReq(
          m.PartnerRecovery.FreeBoosts,
          m.PartnerRecovery.Tickets,
          m.PartnerRecovery.PerBoost,
          m.PartnerRecovery.PerTicket
        ),
      m.FeeRateOverride,
      m.WastedFeeOverride
    );

  // ---- stored invoices ----

  // Status and ticket basis travel as strings for the same reason the
  // rounding preference does: they are read by a human, and "issued" survives
  // a renumbering of the enum where 1 does not.
  public const string StatusDraft = "draft";
  public const string StatusIssued = "issued";
  public const string StatusVoid = "void";

  public static string ToWire(this InvoiceStatus s) =>
    s switch
    {
      InvoiceStatus.Issued => StatusIssued,
      InvoiceStatus.Void => StatusVoid,
      _ => StatusDraft,
    };

  public const string BasisStatusToday = "status_today";
  public const string BasisLedger = "ledger";
  public const string BasisTranscribed = "transcribed_from_issued";

  public static string ToWire(this InvoiceTicketBasis b) =>
    b switch
    {
      InvoiceTicketBasis.Ledger => BasisLedger,
      InvoiceTicketBasis.TranscribedFromIssued => BasisTranscribed,
      _ => BasisStatusToday,
    };

  // Unknown falls to StatusToday, which is how July and August were actually
  // produced and therefore the honest default for a new month.
  public static InvoiceTicketBasis ToTicketBasis(string s) =>
    s switch
    {
      var x when string.Equals(x, BasisLedger, StringComparison.OrdinalIgnoreCase) =>
        InvoiceTicketBasis.Ledger,
      var x when string.Equals(x, BasisTranscribed, StringComparison.OrdinalIgnoreCase) =>
        InvoiceTicketBasis.TranscribedFromIssued,
      _ => InvoiceTicketBasis.StatusToday,
    };

  // Dates cross the wire as dd-MM-yyyy, matching the rest of this codebase's
  // API surface (see argon's pnl page). PeriodMonth is a full date whose day
  // is always the 1st rather than a month string, because it is a DateOnly on
  // both sides and inventing a second format for it would only add a place to
  // get it wrong.
  public const string DateFormat = "dd-MM-yyyy";

  public static DateOnly ToDate(string s) =>
    DateOnly.ParseExact(s, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);

  public static string ToWire(this DateOnly d) =>
    d.ToString(DateFormat, CultureInfo.InvariantCulture);

  public static InvoiceDocumentDraft ToDomain(this SaveInvoiceDraftReq req) =>
    new()
    {
      // Normalized to the 1st: the month is the identity of the invoice, and
      // a caller sending the 15th must not create a second August.
      PeriodMonth = new DateOnly(ToDate(req.PeriodMonth).Year, ToDate(req.PeriodMonth).Month, 1),
      Seq = req.Seq,
      TicketBasis = ToTicketBasis(req.TicketBasis),
      IssueDate = ToDate(req.IssueDate),
      DueDate = ToDate(req.DueDate),
      Inputs = req.Inputs.ToDomain(),
    };

  // A transcribed month is always TranscribedFromIssued — the caller does not
  // get to say otherwise. This is the one write path that stores figures the
  // current engine did not produce, and the basis is how an auditor tells
  // those rows apart from the ones it did.
  public static InvoiceDocumentDraft ToDomain(this TranscribeInvoiceReq req) =>
    new()
    {
      PeriodMonth = new DateOnly(ToDate(req.PeriodMonth).Year, ToDate(req.PeriodMonth).Month, 1),
      Seq = req.Seq,
      TicketBasis = InvoiceTicketBasis.TranscribedFromIssued,
      IssueDate = ToDate(req.IssueDate),
      DueDate = ToDate(req.DueDate),
      Inputs = req.Inputs.ToDomain(),
    };

  public static InvoiceAttestation ToDomain(this TranscribeAttestReq req) =>
    new()
    {
      Tickets = req.Tickets,
      Revenue = req.Revenue,
      NetProfit = req.NetProfit,
      // Case-insensitive because a suffix is a single letter a human types,
      // and "c" and "C" are the same partner.
      Amounts = new Dictionary<string, decimal>(req.Amounts, StringComparer.OrdinalIgnoreCase),
    };

  public static InvoiceSummaryRes ToRes(this InvoiceDocumentSummary s) =>
    new(
      s.Id,
      s.Record.PeriodMonth.ToWire(),
      s.Record.Seq,
      s.Record.Status.ToWire(),
      s.Record.TicketBasis.ToWire(),
      s.Record.EngineVersion,
      s.Record.IssueDate.ToWire(),
      s.Record.DueDate.ToWire(),
      s.NetProfit,
      s.PoolTotal,
      s.CreatedAt,
      s.IssuedAt
    );

  // The frozen halves are deserialized here rather than passed through as raw
  // JSON strings so the response stays a typed contract the SDK generator can
  // see.
  //
  // This is a READ of stored bytes. The calculator is not called, for an
  // issued invoice or any other — which is what makes "open June and see
  // exactly what June paid" true regardless of what the engine does later.
  public static InvoiceDocumentRes? ToRes(this InvoiceDocument? d)
  {
    if (d is null)
      return null;

    var inputs = d.ToInputs();
    var computed = d.ToComputed();
    return new(
      d.Id,
      d.Record.PeriodMonth.ToWire(),
      d.Record.Seq,
      d.Record.Status.ToWire(),
      d.Record.TicketBasis.ToWire(),
      d.Record.EngineVersion,
      d.Record.IssueDate.ToWire(),
      d.Record.DueDate.ToWire(),
      inputs.ToReq(),
      computed.ToRes(),
      d.CreatedAt,
      d.CreatedBy,
      d.IssuedAt,
      d.IssuedBy,
      d.VoidedAt,
      d.VoidedBy,
      d.VoidReason
    );
  }

  public static InvoiceDriftRes ToRes(this InvoiceDrift d) =>
    new(
      d.Id,
      d.HasDrift,
      d.FrozenEngineVersion,
      d.CurrentEngineVersion,
      d.Fields.Select(f => new InvoiceDriftFieldRes(f.Path, f.Frozen, f.Current, f.Delta))
    );
}
