namespace Domain.Invoice;

// The computed invoice — the C# shape of `compute()`'s return value in
// invoices/calc.ts. The TypeScript spreads the input back over the output
// (`...m`); here the input hangs off `Input` instead, so a frozen invoice can
// store the two halves separately and a drift check can re-run the engine over
// the same inputs and diff only the outputs.

public record InvoiceFxResult
{
  public required decimal TotalFundedRm { get; init; }

  public required decimal TotalFundedSgd { get; init; }

  // Carried at FULL precision and applied per route. FxRatePrinted is the 5dp
  // value the PDF shows; multiplying by it would move cents.
  public required decimal FxRate { get; init; }

  public required decimal FxRatePrinted { get; init; }
}

public record InvoiceFeeResult
{
  public required decimal TotalPaymentFees { get; init; }

  public required decimal FeeRatePct { get; init; }
}

public record InvoicePriorityRouteResult
{
  public required int Paid { get; init; }

  public required decimal Fee { get; init; }

  public required int Free { get; init; }

  public required decimal Gross { get; init; }

  public required decimal FeeCost { get; init; }

  public required decimal Net { get; init; }
}

public record InvoiceRouteResult
{
  public required string Key { get; init; }

  public required string Label { get; init; }

  public required string Short { get; init; }

  public required int Tickets { get; init; }

  public required decimal Revenue { get; init; }

  public required decimal FareRm { get; init; }

  public required InvoiceTerminatedInput Terminated { get; init; }

  public required decimal TicketFaresRm { get; init; }

  public required decimal TicketFaresSgd { get; init; }

  public required decimal ProcessingFee { get; init; }

  public required decimal DirectCost { get; init; }

  public required decimal Contribution { get; init; }

  public required decimal MarginPct { get; init; }

  public required decimal TerminatedNet { get; init; }

  public required InvoicePriorityRouteResult Priority { get; init; }

  public required InvoicePriceLine[] PriceLines { get; init; }

  public required decimal SurchargeGross { get; init; }

  // always <= 0
  public required decimal DiscountGross { get; init; }
}

public record InvoiceTotals
{
  public required int Tickets { get; init; }

  public required decimal Revenue { get; init; }

  // the blended price per ticket, the figure the user asks for by name
  public required decimal PricePerTicket { get; init; }

  public required decimal TicketFaresRm { get; init; }

  public required decimal TicketFaresSgd { get; init; }

  public required decimal ProcessingFee { get; init; }

  public required decimal DirectCost { get; init; }

  public required decimal Contribution { get; init; }

  public required decimal TotalMarginPct { get; init; }
}

public record InvoiceAdjustments
{
  public required decimal TerminatedNet { get; init; }

  public required int TerminatedCount { get; init; }

  // what the invoice uses — the override where one is pinned
  public required decimal WastedFee { get; init; }

  // what the formula gives. Kept alongside so the variance stays visible
  // rather than being absorbed by the override.
  public required decimal WastedFeeComputed { get; init; }

  public required decimal Infrastructure { get; init; }
}

public record InvoicePriorityResult
{
  public required decimal Gross { get; init; }

  public required decimal FeeCost { get; init; }

  public required decimal Net { get; init; }

  public required int Paid { get; init; }

  public required int Free { get; init; }

  public required decimal Kept { get; init; }

  public required int KeptCount { get; init; }
}

public record InvoiceSurchargeResult
{
  public required decimal Gross { get; init; }

  public required decimal Discounts { get; init; }

  public required InvoiceSurchargeCoverage Coverage { get; init; }

  public required decimal CoveragePct { get; init; }
}

public record InvoiceAncillary
{
  public required InvoicePriorityResult Priority { get; init; }

  // reported, never added — already inside Revenue
  public required InvoiceSurchargeResult Surcharge { get; init; }

  public required InvoiceWithdrawalFeeInput WithdrawalFee { get; init; }

  public required decimal Promotional { get; init; }

  public required decimal NetTransfers { get; init; }

  public required InvoiceDuplicatesInput Duplicates { get; init; }

  // additive lines only: priority net + kept + withdrawal fee - promotional - transfers
  public required decimal Total { get; init; }
}

public record InvoiceRecoveryResult
{
  public required int FreeBoosts { get; init; }

  public required int Tickets { get; init; }

  public required decimal PerBoost { get; init; }

  public required decimal PerTicket { get; init; }

  public required decimal Boosts { get; init; }

  public required decimal TicketsAmount { get; init; }

  public required decimal Total { get; init; }

  public required bool Applies { get; init; }
}

public record InvoiceShare
{
  public required string Name { get; init; }

  public required string Suffix { get; init; }

  public required InvoiceRoundingPreference RoundingPreference { get; init; }

  public required decimal Pct { get; init; }

  // the agreed profit share, unchanged by the recovery
  public required decimal Earned { get; init; }

  // already collected outside the system — held, so not paid again
  public required decimal Advance { get; init; }

  // what we actually transfer
  public required decimal Amount { get; init; }
}

public record InvoiceResult
{
  public required decimal NetProfit { get; init; }

  public required decimal NetMarginPct { get; init; }

  public required decimal ShareBase { get; init; }

  public required decimal MarketingSharePool { get; init; }

  public required InvoiceShare[] Shares { get; init; }
}

public record InvoiceComputed
{
  public required InvoiceMonthInput Input { get; init; }

  public required InvoiceFxResult Fx { get; init; }

  public required InvoiceFeeResult Fee { get; init; }

  public required InvoiceRouteResult[] Routes { get; init; }

  public required InvoiceTotals Totals { get; init; }

  public required InvoiceAdjustments Adjustments { get; init; }

  public required InvoiceAncillary Ancillary { get; init; }

  public required InvoiceRecoveryResult Recovery { get; init; }

  public required InvoiceResult Result { get; init; }
}
