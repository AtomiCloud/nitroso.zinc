namespace Domain.Invoice;

// The complete input to the invoice engine — the C# shape of `MonthInput` in
// invoices/calc.ts. Everything the calculator needs and nothing it does not:
// no repositories, no clock, no settings lookup. Assembling this from the
// database, the settings queue and the top-up ledger is somebody else's job,
// which is what makes the calculator a pure function that can be pinned
// against the issued PDFs.

public record InvoiceTopup
{
  public required string Date { get; init; }

  public required decimal Rm { get; init; }

  public required decimal Sgd { get; init; }
}

public record InvoiceTerminatedInput
{
  public required int Count { get; init; }

  public required decimal KeptRevenue { get; init; }

  public required decimal HalfFareSgd { get; init; }
}

public record InvoiceRouteInput
{
  public required string Key { get; init; }

  public required string Label { get; init; }

  public required string Short { get; init; }

  public required int Tickets { get; init; }

  public required decimal Revenue { get; init; }

  public required decimal FareRm { get; init; }

  public required InvoiceTerminatedInput Terminated { get; init; }
}

// One priced line inside a booking's stored price breakdown. Policy lines are
// surcharges (positive), discount lines are giveaways (negative). Both are
// ALREADY inside the booking's final price and therefore already inside
// Revenue — they are reported to explain the blended price per ticket, and
// must never be added to it.
public enum InvoicePriceLineKind : byte
{
  Policy = 0,
  Discount = 1,
}

public record InvoicePriceLine
{
  public required InvoicePriceLineKind Kind { get; init; }

  public required string Name { get; init; }

  public required int Count { get; init; }

  public required decimal Delta { get; init; }
}

public record InvoicePriorityRouteInput
{
  public required int Paid { get; init; }

  public required decimal Fee { get; init; }

  public required int Free { get; init; }
}

// Flat SGD 10.00 queue-jump fee. Charged as its OWN ledger transaction
// (TransactionType.PriorityFee), NOT folded into the booking's request
// amount, so it is genuinely additive revenue.
public record InvoicePriorityInput
{
  public required IReadOnlyDictionary<string, InvoicePriorityRouteInput> PerRoute { get; init; }

  // Priority fees on bookings that did NOT complete and were not refunded.
  // Real income, but recognised separately from the completed-booking basis
  // the rest of the invoice uses.
  public required decimal KeptOnCancelled { get; init; }

  public required int KeptOnCancelledCount { get; init; }
}

// Coverage is load-bearing: the PriceBreakdown column only started being
// written mid-July, so surcharge figures are UNDERSTATED where coverage is
// below 100%. Never extrapolate the missing part — print the shortfall.
public record InvoiceSurchargeCoverage
{
  public required int WithBreakdown { get; init; }

  public required int Total { get; init; }
}

public record InvoiceSurchargeInput
{
  public required InvoiceSurchargeCoverage Coverage { get; init; }

  public required IReadOnlyDictionary<string, InvoicePriceLine[]> PerRoute { get; init; }
}

// Fee BunnyBooker keeps on each completed withdrawal (the user receives
// Amount - Fee). Collected on the way OUT, so it carries no inbound gateway
// fee — distinct from WastedFee, which is the inbound cost on money that left
// without buying a ticket.
public record InvoiceWithdrawalFeeInput
{
  public required decimal Income { get; init; }

  public required int WithFee { get; init; }

  public required int Count { get; init; }
}

// Money the partner collected OUTSIDE the system, which we assume and charge
// for. Not revenue — none of it ever reached a BunnyBooker account, so it
// cannot sit in Revenue or move NetProfit. See the calculator for why it is
// an advance against their share rather than a business cost.
public record InvoiceRecoveryInput
{
  // a boost granted at PriorityFee = 0; the rider still paid out-of-system
  public required int FreeBoosts { get; init; }

  // every completed booking on the partner account, assumed resold
  public required int Tickets { get; init; }

  public required decimal PerBoost { get; init; }

  public required decimal PerTicket { get; init; }
}

// Double-booked tickets refunded to the user. Reported for visibility only.
public record InvoiceDuplicatesInput
{
  public required int Count { get; init; }

  public required decimal Refunded { get; init; }
}

public record InvoicePeriod
{
  public required string Label { get; init; }

  public required string MonthName { get; init; }

  public required string Seq { get; init; }
}

public record InvoiceFeesInput
{
  public required decimal Gateway { get; init; }

  public required decimal PaymentMethod { get; init; }
}

public record InvoiceWithdrawalsInput
{
  public required int Count { get; init; }

  public required decimal Total { get; init; }
}

public record InvoiceMonthInput
{
  public required InvoicePeriod Period { get; init; }

  public required string IssueDate { get; init; }

  public required string DueDate { get; init; }

  public required InvoiceTopup[] Topups { get; init; }

  public string TopupNote { get; init; } = string.Empty;

  public required InvoiceFeesInput Fees { get; init; }

  public required decimal GrossDeposits { get; init; }

  public required decimal RefundFeesExcluded { get; init; }

  public required InvoiceRouteInput[] Routes { get; init; }

  public required InvoiceWithdrawalsInput Withdrawals { get; init; }

  public required decimal Infrastructure { get; init; }

  public required decimal MarketingSharePct { get; init; }

  public required InvoicePartner[] Partners { get; init; }

  public required InvoicePriorityInput Priority { get; init; }

  public required InvoiceSurchargeInput Surcharge { get; init; }

  public required InvoiceWithdrawalFeeInput WithdrawalFee { get; init; }

  public required InvoicePromotionalInput Promotional { get; init; }

  // Net of manual BunnyBooker <-> wallet corrections. Signed: positive means
  // BunnyBooker paid out, so it is a cost.
  public required decimal NetTransfers { get; init; }

  public required InvoiceDuplicatesInput Duplicates { get; init; }

  public InvoiceRecoveryInput? PartnerRecovery { get; init; }

  // Pin the blended fee % to the value an already-issued invoice printed.
  // June needs this: the true rate is 5.3346485%, which rounds to 5.3346,
  // but the issued PDF printed AND APPLIED 5.3347.
  public decimal? FeeRateOverride { get; init; }

  // Honour a figure from an issued document that the formula cannot
  // reproduce. June's printed wasted fee is 58.42 where the formula gives
  // 58.4150 — no rate derivable from the printed inputs yields it.
  public decimal? WastedFeeOverride { get; init; }
}

// Credits handed to users — a real giveaway cost, not a fee.
public record InvoicePromotionalInput
{
  public required int Count { get; init; }

  public required decimal Amount { get; init; }
}
