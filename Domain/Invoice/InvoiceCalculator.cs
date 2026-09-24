using static Domain.Invoice.InvoiceRounding;

namespace Domain.Invoice;

// Profit-share calculation engine, ported line-for-line from invoices/calc.ts,
// which was itself reverse-engineered from the two issued June 2026 invoices
// (BB-2026-0601-C / -Z) and validated against every printed figure in them.
//
// Rounding is the fiddly part, and the June sheet is not uniform about it.
// Established by solving the printed figures backwards:
//
//   * FX is applied at FULL precision (0.3197736654...), not the printed
//     0.31977. Proof: 7,810 x full = 2,497.43 (printed).
//     7,810 x 0.31977 = 2,497.40.
//   * The processing-fee rate is applied at the PRINTED 4dp value (5.3347%),
//     not full precision. Proof: 15,507 x 5.3347% = 827.25 (printed);
//     15,507 x 5.3346485% = 827.24.
//   * Totals are sums of the ROUNDED per-route figures — that is how
//     2,497.43 + 6,441.04 reconciles to 8,938.47 exactly.
//
// One irreducible discrepancy remains: June's printed wasted fee is 58.42, but
// 1,095 x 5.3347% = 58.4150 and no rate derivable from the printed inputs
// yields it. Where the input supplies WastedFeeOverride we honour the issued
// document, and the variance is reported rather than hidden.
//
// DIVISION BY ZERO. The TypeScript produces NaN or Infinity for an empty
// month; decimal division throws instead. Every such divisor is guarded to
// zero below and marked `// guard`. This only differs from the oracle on
// inputs that could never be invoiced (no tickets, no revenue, no top-ups, no
// partners), where the oracle's answer was NaN — i.e. not an answer.
public static class InvoiceCalculator
{
  // Priority-fee income arrived as a wallet deposit like any other, so it paid
  // the same inbound gateway fee as ticket revenue. Set false to report
  // ancillary revenue gross instead.
  public const bool ChargeProcessingFeeOnPriority = true;

  public static InvoiceComputed Compute(InvoiceMonthInput m)
  {
    // ---- FX: blended actual, SGD paid per RM received ---------------------
    var totalFundedRm = R(m.Topups.Sum(t => t.Rm));
    var totalFundedSgd = R(m.Topups.Sum(t => t.Sgd));
    // Applied at full precision; FxRatePrinted is the 5dp value shown on the PDF.
    var fxRate = totalFundedRm == 0 ? 0m : totalFundedSgd / totalFundedRm; // guard
    var fxRatePrinted = R(fxRate, 5);

    // ---- Processing fee: blended actual, % of gross deposits ---------------
    var totalPaymentFees = R(m.Fees.Gateway + m.Fees.PaymentMethod);
    // Rounded to the printed 4dp and applied at that value — see header note.
    // FeeRateOverride lets an issued invoice pin the rate it actually used.
    var feeRatePct = m.FeeRateOverride
                     ?? (m.GrossDeposits == 0 ? 0m : R(totalPaymentFees / m.GrossDeposits * 100m, 4)); // guard

    // ---- Per-route economics ----------------------------------------------
    var routes = m.Routes.Select(rt =>
    {
      var ticketFaresRm = R(rt.Tickets * rt.FareRm);
      var ticketFaresSgd = R(ticketFaresRm * fxRate);
      var processingFee = R(rt.Revenue * (feeRatePct / 100m));
      var directCost = R(ticketFaresSgd + processingFee);
      var contribution = R(rt.Revenue - directCost);
      var marginPct = rt.Revenue == 0 ? 0m : R(contribution / rt.Revenue * 100m, 1); // guard
      var terminatedNet = R(rt.Terminated.KeptRevenue - rt.Terminated.HalfFareSgd);

      // ---- ancillary, per route -------------------------------------------
      // Priority fee is additive revenue, so it carries the inbound processing
      // fee exactly as ticket revenue does (see ChargeProcessingFeeOnPriority).
      var p = m.Priority.PerRoute.GetValueOrDefault(rt.Key)
              ?? new InvoicePriorityRouteInput { Paid = 0, Fee = 0, Free = 0 };
      var priorityGross = R(p.Fee);
      var priorityFeeCost = ChargeProcessingFeeOnPriority ? R(priorityGross * (feeRatePct / 100m)) : 0m;
      var priorityNet = R(priorityGross - priorityFeeCost);

      // Surcharges/discounts are already inside Revenue — split out, never added.
      var lines = m.Surcharge.PerRoute.GetValueOrDefault(rt.Key) ?? [];
      var surchargeGross = R(lines.Where(l => l.Kind == InvoicePriceLineKind.Policy).Sum(l => l.Delta));
      var discountGross = R(lines.Where(l => l.Kind == InvoicePriceLineKind.Discount).Sum(l => l.Delta));

      return new InvoiceRouteResult
      {
        Key = rt.Key,
        Label = rt.Label,
        Short = rt.Short,
        Tickets = rt.Tickets,
        Revenue = rt.Revenue,
        FareRm = rt.FareRm,
        Terminated = rt.Terminated,
        TicketFaresRm = ticketFaresRm,
        TicketFaresSgd = ticketFaresSgd,
        ProcessingFee = processingFee,
        DirectCost = directCost,
        Contribution = contribution,
        MarginPct = marginPct,
        TerminatedNet = terminatedNet,
        Priority = new InvoicePriorityRouteResult
        {
          Paid = p.Paid,
          Fee = p.Fee,
          Free = p.Free,
          Gross = priorityGross,
          FeeCost = priorityFeeCost,
          Net = priorityNet,
        },
        PriceLines = lines,
        SurchargeGross = surchargeGross,
        DiscountGross = discountGross,
      };
    }).ToArray();

    // Totals are sums of the ROUNDED per-route figures, matching how the
    // invoice's TOTAL column reconciles with its two route columns.
    decimal Sum(Func<InvoiceRouteResult, decimal> f) => R(routes.Sum(f));

    var tickets = routes.Sum(x => x.Tickets);
    var revenue = Sum(x => x.Revenue);
    var ticketFaresRmTotal = Sum(x => x.TicketFaresRm);
    var ticketFaresSgdTotal = Sum(x => x.TicketFaresSgd);
    var processingFeeTotal = Sum(x => x.ProcessingFee);
    var directCostTotal = Sum(x => x.DirectCost);
    var contributionTotal = Sum(x => x.Contribution);
    var pricePerTicket = tickets == 0 ? 0m : R(revenue / tickets); // guard
    var totalMarginPct = revenue == 0 ? 0m : R(contributionTotal / revenue * 100m, 1); // guard

    // ---- Adjustments -------------------------------------------------------
    var terminatedNetTotal = Sum(x => x.TerminatedNet);
    var terminatedCount = routes.Sum(x => x.Terminated.Count);
    // Inbound fee paid on money that left as a withdrawal without buying a ticket
    var wastedFeeComputed = R(m.Withdrawals.Total * (feeRatePct / 100m));
    var wastedFee = m.WastedFeeOverride ?? wastedFeeComputed;

    // ---- Ancillary revenue -------------------------------------------------
    // Everything BunnyBooker earns that is NOT the ticket margin. Two of these
    // are additive (priority fee, withdrawal fee); the surcharge/discount pair
    // is already inside Revenue and is reported, not added.
    var priorityGrossTotal = Sum(x => x.Priority.Gross);
    var priorityFeeCostTotal = Sum(x => x.Priority.FeeCost);
    var priorityNetTotal = Sum(x => x.Priority.Net);
    var priorityPaid = routes.Sum(x => x.Priority.Paid);
    var priorityFree = routes.Sum(x => x.Priority.Free);

    // Priority fees kept on bookings the user cancelled. No route split exists
    // for these (they never completed), so they sit at the total level.
    var priorityKept = R(m.Priority.KeptOnCancelled);

    var surchargeGrossTotal = Sum(x => x.SurchargeGross);
    var discountGrossTotal = Sum(x => x.DiscountGross);
    var surchargeCoveragePct = m.Surcharge.Coverage.Total == 0
      ? 0m
      : R((decimal)m.Surcharge.Coverage.WithBreakdown / m.Surcharge.Coverage.Total * 100m, 1);

    var withdrawalFeeIncome = R(m.WithdrawalFee.Income);
    var promotional = R(m.Promotional.Amount);
    var netTransfers = R(m.NetTransfers);

    // Additive only. Surcharge and discount are deliberately absent — adding
    // them would double-count money already inside Revenue.
    var ancillaryNet = R(priorityNetTotal + priorityKept + withdrawalFeeIncome - promotional - netTransfers);

    var netProfit = R(contributionTotal + terminatedNetTotal + ancillaryNet - wastedFee - m.Infrastructure);
    var netMarginPct = revenue == 0 ? 0m : R(netProfit / revenue * 100m, 1); // guard

    // ---- Partner out-of-system recovery ------------------------------------
    // Money the partner ALREADY COLLECTED outside BunnyBooker and is holding.
    // It never touched our accounts, so it is NOT revenue and does NOT move
    // NetProfit — the profit split is computed on the full NetProfit exactly as
    // before, and this is then deducted from what we PAY OUT, because they have
    // already been paid that much in cash.
    //
    // It is an ADVANCE AGAINST THEIR SHARE, not a cost of the business. Getting
    // this wrong is easy and expensive: deducting it from the pool BEFORE the
    // split only claws back HALF of it (we would eat the other half), and the
    // payable would go UP relative to an invoice with no recovery at all, which
    // is backwards. The payable must DROP by the full amount they are holding.
    var pr = m.PartnerRecovery;
    var recoveryBoosts = R((pr?.FreeBoosts ?? 0) * (pr?.PerBoost ?? 0m));
    var recoveryTickets = R((pr?.Tickets ?? 0) * (pr?.PerTicket ?? 0m));
    var recoveryTotal = R(recoveryBoosts + recoveryTickets);

    // ---- Profit share ------------------------------------------------------
    // The split itself is untouched by the recovery: full NetProfit, as agreed.
    var shareBase = netProfit;
    var marketingSharePool = R(shareBase * (m.MarketingSharePct / 100m));
    var partnerCount = m.Partners.Length;
    var perPartnerPct = partnerCount == 0 ? 0m : m.MarketingSharePct / partnerCount; // guard
    var rawShare = shareBase * (perPartnerPct / 100m);

    // The pool is rounded to the cent, so the per-partner shares must be
    // ALLOCATED out of it rather than rounded independently — otherwise both
    // shares can round the same way and the split no longer sums to the pool
    // (Aug 2026: pool 16,835.93, but 8,417.9625 rounds to 8,417.96 twice = a
    // cent short). Largest-remainder: everyone floors to the cent, then the
    // leftover cents go one each to the partners whose RoundingPreference is
    // Up — which is exactly how June resolved its 4,135.585 half-cent.
    var poolCents = R(marketingSharePool * 100m, 0);
    var baseCents = decimal.Floor(R(rawShare * 100m, 6));
    var leftover = poolCents - baseCents * partnerCount;

    // Stable: Up-preference partners first, then original position.
    var order = m.Partners
      .Select((p, i) => (Partner: p, Index: i))
      .OrderBy(x => x.Partner.RoundingPreference == InvoiceRoundingPreference.Up ? 0 : 1)
      .ThenBy(x => x.Index)
      .ToArray();

    var extra = new Dictionary<int, decimal>();
    foreach (var (_, i) in order)
    {
      var give = leftover > 0 ? 1m : 0m;
      extra[i] = give;
      leftover -= give;
    }

    // The advance is split evenly across partners in cents, so the amounts owed
    // still reconcile exactly against the recovery total (no stray cent).
    var advCents = R(recoveryTotal * 100m, 0);
    var advBase = partnerCount == 0 ? 0m : decimal.Floor(advCents / partnerCount); // guard
    var advLeft = advCents - advBase * partnerCount;
    var advExtra = new Dictionary<int, decimal>();
    foreach (var (_, i) in order)
    {
      var give = advLeft > 0 ? 1m : 0m;
      advExtra[i] = give;
      advLeft -= give;
    }

    var shares = m.Partners.Select((p, i) =>
    {
      var earned = (baseCents + extra.GetValueOrDefault(i)) / 100m;
      var advance = (advBase + advExtra.GetValueOrDefault(i)) / 100m;
      return new InvoiceShare
      {
        Name = p.Name,
        Suffix = p.Suffix,
        RoundingPreference = p.RoundingPreference,
        Pct = perPartnerPct,
        Earned = earned,
        Advance = advance,
        Amount = R(earned - advance),
      };
    }).ToArray();

    return new InvoiceComputed
    {
      Input = m,
      Fx = new InvoiceFxResult
      {
        TotalFundedRm = totalFundedRm,
        TotalFundedSgd = totalFundedSgd,
        FxRate = fxRate,
        FxRatePrinted = fxRatePrinted,
      },
      Fee = new InvoiceFeeResult { TotalPaymentFees = totalPaymentFees, FeeRatePct = feeRatePct },
      Routes = routes,
      Totals = new InvoiceTotals
      {
        Tickets = tickets,
        Revenue = revenue,
        PricePerTicket = pricePerTicket,
        TicketFaresRm = ticketFaresRmTotal,
        TicketFaresSgd = ticketFaresSgdTotal,
        ProcessingFee = processingFeeTotal,
        DirectCost = directCostTotal,
        Contribution = contributionTotal,
        TotalMarginPct = totalMarginPct,
      },
      Adjustments = new InvoiceAdjustments
      {
        TerminatedNet = terminatedNetTotal,
        TerminatedCount = terminatedCount,
        WastedFee = wastedFee,
        WastedFeeComputed = wastedFeeComputed,
        Infrastructure = m.Infrastructure,
      },
      Ancillary = new InvoiceAncillary
      {
        Priority = new InvoicePriorityResult
        {
          Gross = priorityGrossTotal,
          FeeCost = priorityFeeCostTotal,
          Net = priorityNetTotal,
          Paid = priorityPaid,
          Free = priorityFree,
          Kept = priorityKept,
          KeptCount = m.Priority.KeptOnCancelledCount,
        },
        Surcharge = new InvoiceSurchargeResult
        {
          Gross = surchargeGrossTotal,
          Discounts = discountGrossTotal,
          Coverage = m.Surcharge.Coverage,
          CoveragePct = surchargeCoveragePct,
        },
        WithdrawalFee = m.WithdrawalFee with { Income = withdrawalFeeIncome },
        Promotional = promotional,
        NetTransfers = netTransfers,
        Duplicates = m.Duplicates,
        Total = ancillaryNet,
      },
      Recovery = new InvoiceRecoveryResult
      {
        FreeBoosts = pr?.FreeBoosts ?? 0,
        Tickets = pr?.Tickets ?? 0,
        PerBoost = pr?.PerBoost ?? 0m,
        PerTicket = pr?.PerTicket ?? 0m,
        Boosts = recoveryBoosts,
        TicketsAmount = recoveryTickets,
        Total = recoveryTotal,
        Applies = recoveryTotal > 0,
      },
      Result = new InvoiceResult
      {
        NetProfit = netProfit,
        NetMarginPct = netMarginPct,
        ShareBase = shareBase,
        MarketingSharePool = marketingSharePool,
        Shares = shares,
      },
    };
  }
}
