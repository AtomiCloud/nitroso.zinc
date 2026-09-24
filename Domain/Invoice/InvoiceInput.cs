namespace Domain.Invoice;

// Everything the partner-invoice engine needs for one calendar month, derived
// from zinc's own records so nobody has to assemble it by hand. The shape
// mirrors `MonthInput` in the invoices/ toolchain (calc.ts) field-for-field:
// that engine remains the authority on the arithmetic, this is purely the
// gathering step it used to depend on a human for.
//
// GRAIN: the invoice reports per DIRECTION (JB->Woodlands, Woodlands->JB),
// which is finer than the existing analysis endpoints report for anything
// except tickets/gross/ktmbCost. Every measure here is per-direction where
// the invoice prints it per-direction.
//
// BUCKETING: SGT calendar month, on the source event — payment CreatedAt,
// booking CompletedAt, gateway-fee TransactedAt, withdrawal CompletedAt.
// Matches the existing P&L endpoints exactly so the two reconcile.
//
// SNAPSHOT WARNING: booking status is mutated in place, so a month recomputed
// later will NOT match the invoice issued at the time (June: 2,713 tickets
// issued, 2,672 in Completed today — the rest were later refunded or
// terminated). See invoices/PROVENANCE.md. Callers that intend to issue a
// document must freeze this payload, not re-derive it.

// Which SGT month to gather. Single month rather than a range: the invoice is
// a monthly document and the period label, issue date and due date all derive
// from it.
public record InvoiceInputQuery
{
  // first day of the SGT month being invoiced
  public required DateOnly Month { get; init; }
}

// Direction as the invoice prints it. Mirrors the DB's Bookings."Direction"
// (1 = JToW, 2 = WToJ) rather than the domain TrainDirection enum (0 = JToW,
// 1 = WToJ) — the two disagree and the DB column is what we aggregate on.
public enum InvoiceDirection
{
  JbToWoodlands = 1,
  WoodlandsToJb = 2,
}

// One DB-side daily subtotal, sparse per source: each UNION ALL arm fills
// only its own measures and zeroes the rest. Keeping the SGT date and the
// direction until this pure boundary lets the calculator own month bucketing
// and the per-route fold while the repository keeps every source scan
// aggregated. Same shape of contract as PnlTerminalDailySum.
public record InvoiceInputDailySum
{
  public required DateOnly Date { get; init; }

  // 0 for measures that carry no direction (deposits, gateway fees,
  // withdrawals); 1/2 for booking-sourced measures
  public required int Direction { get; init; }

  // ---- money in (no direction) ----

  // captured payment amounts (Payments.Status = 'SUCCEEDED')
  public required decimal Deposits { get; init; }

  // gateway fees on accepting deposits (SourceType = Payment)
  public required decimal PaymentMethodFees { get; init; }

  // account-level gateway billings (SourceType = AccountFee)
  public required decimal GatewayFees { get; init; }

  // fees charged on refunds (SourceType = Refund), excluded from the invoice's
  // processing-fee base and reported separately
  public required decimal RefundFees { get; init; }

  // ---- completed bookings (per direction) ----

  public required int CompletedCount { get; init; }

  // the booking's REQUEST transaction amount only. Surcharges and discounts
  // are already baked in; the priority fee is NOT (it is its own ledger row)
  // and is reported separately below.
  public required decimal CompletedRevenue { get; init; }

  // boosts that were paid for
  public required int PriorityPaidCount { get; init; }

  public required decimal PriorityFee { get; init; }

  // boosts granted at no charge — earn nothing, shown for context (and they
  // drive the partner-recovery assumption)
  public required int PriorityFreeCount { get; init; }

  // ---- terminated bookings (per direction) ----

  public required int TerminatedCount { get; init; }

  // everything collected on bookings that terminated. The invoice keeps the
  // non-refunded share of this; the refund split is applied by the calculator
  // so the rate lives in exactly one place.
  public required decimal TerminatedCollected { get; init; }

  // ---- withdrawals (no direction) ----

  public required int WithdrawalCount { get; init; }

  public required decimal WithdrawalTotal { get; init; }

  // fee BunnyBooker keeps on each completed withdrawal (user receives
  // Amount - Fee). Collected on the way OUT, so it carries no inbound gateway
  // fee — distinct from the wastedFee line.
  public required decimal WithdrawalFeeIncome { get; init; }

  public required int WithdrawalWithFeeCount { get; init; }

  // ---- KTMB card top-ups (no direction) ----
  //
  // Money moved onto the Airwallex issuing card to buy tickets with. Charged
  // in MYR, billed to us in SGD; the ratio of the two over the month IS the
  // rate the invoice converts fares at. A measured rate, not a quoted one.
  //
  // Kept as two separate sums rather than a rate: the blend has to be
  // sum-over-sum across the whole month, and averaging per-day rates would
  // weight a RM 500 day the same as a RM 10,000 one.
  public required decimal TopupMyr { get; init; }

  public required decimal TopupSgd { get; init; }
}

// One route's figures as the invoice prints them.
public record InvoiceInputRoute
{
  // "jbw" / "wjb" — the invoice data files' route keys
  public required string Key { get; init; }

  public required InvoiceDirection Direction { get; init; }

  public required int Tickets { get; init; }

  public required decimal Revenue { get; init; }

  public required InvoiceInputTerminated Terminated { get; init; }

  public required InvoiceInputPriority Priority { get; init; }
}

public record InvoiceInputTerminated
{
  public required int Count { get; init; }

  // the share of collected money kept after refunding the rider
  public required decimal KeptRevenue { get; init; }
}

public record InvoiceInputPriority
{
  public required int Paid { get; init; }

  public required decimal Fee { get; init; }

  public required int Free { get; init; }
}

public record InvoiceInputWithdrawals
{
  public required int Count { get; init; }

  public required decimal Total { get; init; }

  public required decimal Income { get; init; }

  public required int WithFee { get; init; }
}

// The month's KTMB card funding, as a single blended pair.
//
// One day-bucketed row per top-up is deliberately NOT reported. The invoice
// prints a table of individual top-ups with their dates, but that table is
// presentation: the only figure the arithmetic uses is the ratio of the two
// totals. Reporting the pair keeps this endpoint's contract about what the
// engine needs, and a caller wanting the itemized table can read the ledger.
public record InvoiceInputTopups
{
  public required decimal Myr { get; init; }

  public required decimal Sgd { get; init; }
}

public record InvoiceInputFees
{
  // account-level Airwallex billings (SourceType = AccountFee)
  public required decimal Gateway { get; init; }

  // per-payment acceptance fees (SourceType = Payment)
  public required decimal PaymentMethod { get; init; }
}

// The gathered month. Deliberately NOT a computed invoice: no profit, no
// split, no rounding decisions. Those belong to the engine.
public record InvoiceInputRow
{
  // "MM-yyyy"
  public required string Month { get; init; }

  public required decimal GrossDeposits { get; init; }

  public required InvoiceInputFees Fees { get; init; }

  // fees charged on refunds — reported so the processing-fee base can exclude
  // them explicitly rather than silently
  public required decimal RefundFeesExcluded { get; init; }

  public required InvoiceInputRoute[] Routes { get; init; }

  public required InvoiceInputWithdrawals Withdrawals { get; init; }

  // The last figure that was still collected by hand. An admin downloaded the
  // Airwallex issuing ledger every month and transcribed the totals; with this
  // reported here, nothing about a month is hand-assembled.
  //
  // Zero is possible and means "no top-up posted in this month", which is a
  // real answer for a month invoiced before the issuing sweep existed. The
  // caller has to notice that and either back-enter the top-ups or say so —
  // it must not be read as an FX rate of zero.
  public required InvoiceInputTopups Topups { get; init; }
}

public static class InvoiceInputCalculator
{
  // Share of a terminated booking's collected amount refunded to the rider.
  // The remainder is kept as revenue. Verified against production June:
  // collected 856.00 / 848.00 by direction, kept 428.00 / 424.00 on the
  // issued invoice — exactly half. One named constant so the day this policy
  // changes there is a single place to change it.
  public const decimal TerminationRefundRate = 0.50m;

  // Route keys as the invoice data files spell them.
  public static string RouteKey(InvoiceDirection direction) =>
    direction switch
    {
      InvoiceDirection.JbToWoodlands => "jbw",
      InvoiceDirection.WoodlandsToJb => "wjb",
      _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

  // Fold sparse daily rows into the one month requested. Routes are always
  // emitted for both directions, in a stable order, even when a direction
  // sold nothing that month — the invoice prints both lines and a missing
  // route would silently drop a section rather than show a zero.
  public static InvoiceInputRow Gather(IEnumerable<InvoiceInputDailySum> days, DateOnly month)
  {
    var inMonth = days.Where(d => d.Date.Year == month.Year && d.Date.Month == month.Month)
      .ToArray();

    var routes = new[] { InvoiceDirection.JbToWoodlands, InvoiceDirection.WoodlandsToJb }
      .Select(dir =>
      {
        var rows = inMonth.Where(d => d.Direction == (int)dir).ToArray();
        var collected = rows.Sum(d => d.TerminatedCollected);
        return new InvoiceInputRoute
        {
          Key = RouteKey(dir),
          Direction = dir,
          Tickets = rows.Sum(d => d.CompletedCount),
          Revenue = rows.Sum(d => d.CompletedRevenue),
          Terminated = new InvoiceInputTerminated
          {
            Count = rows.Sum(d => d.TerminatedCount),
            KeptRevenue = collected * (1m - TerminationRefundRate),
          },
          Priority = new InvoiceInputPriority
          {
            Paid = rows.Sum(d => d.PriorityPaidCount),
            Fee = rows.Sum(d => d.PriorityFee),
            Free = rows.Sum(d => d.PriorityFreeCount),
          },
        };
      })
      .ToArray();

    return new InvoiceInputRow
    {
      Month = month.ToString("MM-yyyy"),
      GrossDeposits = inMonth.Sum(d => d.Deposits),
      Fees = new InvoiceInputFees
      {
        Gateway = inMonth.Sum(d => d.GatewayFees),
        PaymentMethod = inMonth.Sum(d => d.PaymentMethodFees),
      },
      RefundFeesExcluded = inMonth.Sum(d => d.RefundFees),
      Routes = routes,
      Withdrawals = new InvoiceInputWithdrawals
      {
        Count = inMonth.Sum(d => d.WithdrawalCount),
        Total = inMonth.Sum(d => d.WithdrawalTotal),
        Income = inMonth.Sum(d => d.WithdrawalFeeIncome),
        WithFee = inMonth.Sum(d => d.WithdrawalWithFeeCount),
      },
      Topups = new InvoiceInputTopups
      {
        Myr = inMonth.Sum(d => d.TopupMyr),
        Sgd = inMonth.Sum(d => d.TopupSgd),
      },
    };
  }
}

public interface IInvoiceInputRepository
{
  Task<CSharp_Result.Result<InvoiceInputRow>> Gather(InvoiceInputQuery query);
}
