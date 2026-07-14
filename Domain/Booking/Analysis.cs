using CSharp_Result;
using Domain.Timings;

namespace Domain.Booking;

// Sales/revenue analysis for the admin "Analysis" page. Revenue figures are
// GROSS; cost figures are what leaves BunnyBooker's pocket: the KTMB ticket
// cost (admin-configured, effective-dated per direction) and Airwallex's own
// gateway fees (captured after the fact by the gateway-fee sync). Internal
// fees (deposit/withdrawal/priority/termination) are revenue TO BunnyBooker
// and are deliberately excluded from the cost side of net.
public record BookingAnalysisQuery
{
  // SGT calendar-date range (inclusive), same convention as booking_stats:
  // travel/CompletedAt instants are bucketed on their SGT (UTC+8) wall-clock
  // date; null = unbounded
  public DateOnly? After { get; init; }

  public DateOnly? Before { get; init; }
}

// One completed-revenue row: bookings completed on this SGT date, for this
// departure slot. GrossRevenue = the booking cost collected at completion
// (BookingReserve -> BunnyBooker); KtmbCost = the sum of each booking's KTMB
// cost — the ACTUAL amount paid when recorded (SGD as-is, MYR × the FX rate
// effective at its CompletedAt; see KtmbActualCost), else the effective
// per-direction estimate at its CompletedAt (0 when none configured).
public record BookingAnalysisRow
{
  // SGT date of CompletedAt (matches booking_stats' SGT date convention)
  public required DateOnly Date { get; init; }

  public required TrainDirection Direction { get; init; }

  public required TimeOnly Time { get; init; }

  public required int TicketsCompleted { get; init; }

  public required decimal GrossRevenue { get; init; }

  public required decimal KtmbCost { get; init; }
}

// Airwallex deposits captured in the range: how many intents captured money
// and the total captured amount
public record DepositSummary
{
  public required int Count { get; init; }

  public required decimal Captured { get; init; }
}

// BunnyBooker's own fee revenue in the range, per fee kind
public record InternalFees
{
  public required decimal Deposit { get; init; }

  public required decimal Withdrawal { get; init; }

  // net: priority fees charged minus priority fees refunded (both ledger
  // rows share TransactionType.PriorityFee and are told apart by account)
  public required decimal Priority { get; init; }

  // kept on termination: the terminated bookings' collected cost minus what
  // was refunded (the ledger's BookingTerminated row only carries the refund)
  public required decimal Termination { get; init; }
}

// how many captured payment intents in range already have gateway fee rows
// — fees post with delay, so the UI can show sync coverage
public record GatewayFeeCoverage
{
  public required int PaymentsWithFee { get; init; }

  public required int PaymentsTotal { get; init; }
}

// Airwallex's own fees in the range (from the synced gateway-fee rows),
// bucketed by which direction the money moved
public record GatewayFeeSummary
{
  // fees on captured payment intents (money in)
  public required decimal Payments { get; init; }

  // fees on payout transfers + card refunds (money out)
  public required decimal Payouts { get; init; }

  public required GatewayFeeCoverage Coverage { get; init; }
}

// how many completed bookings in range carry an ACTUAL KTMB-paid amount —
// tin captures/backfills with delay, so the UI can show capture coverage
// (mirrors GatewayFeeCoverage)
public record KtmbActualCoverage
{
  public required int WithActual { get; init; }

  public required int Total { get; init; }
}

// per-direction slice of the completed rows (summary- and month-level)
public record DirectionBreakdown
{
  public required TrainDirection Direction { get; init; }

  public required int Tickets { get; init; }

  public required decimal Gross { get; init; }

  public required decimal KtmbCost { get; init; }
}

// One SGT calendar month of the range.
//
// NET FORMULA: Net = Gross − KtmbCost − GatewayPaymentFees −
// GatewayPayoutFees. InternalFees is BunnyBooker's own fee REVENUE in the
// month (deposit + withdrawal + net priority + net termination) — it is
// reported for context but deliberately NOT part of the cost side.
public record MonthlyAnalysisRow
{
  // "MM-yyyy" (SGT calendar month)
  public required string Month { get; init; }

  public required decimal Gross { get; init; }

  public required decimal KtmbCost { get; init; }

  public required decimal GatewayPaymentFees { get; init; }

  public required decimal GatewayPayoutFees { get; init; }

  public required decimal InternalFees { get; init; }

  public required decimal Net { get; init; }

  public required DirectionBreakdown[] ByDirection { get; init; }
}

// month-keyed external sums the data layer feeds the monthly rollup
public record MonthlyExternalSums
{
  public required decimal GatewayPaymentFees { get; init; }

  public required decimal GatewayPayoutFees { get; init; }

  public required decimal InternalFees { get; init; }
}

// one pricing component's aggregate over the range — ranks which policies /
// discounts / the priority boost make or lose money. Kind: "policy" (signed
// delta as configured), "discount" (negative), "priorityFee" (positive, from
// the PriorityFee column of completed bookings).
public record PriceComponentSummary
{
  public required string Kind { get; init; }

  public required string Name { get; init; }

  public required int TimesApplied { get; init; }

  public required decimal TotalDelta { get; init; }
}

// breakdown persistence started with this release — old bookings have no
// stored breakdown and are excluded from Components, never guessed
public record ComponentsCoverage
{
  public required int WithBreakdown { get; init; }

  public required int Total { get; init; }
}

public record BookingAnalysisSummary
{
  public required int TotalTickets { get; init; }

  public required decimal TotalGross { get; init; }

  public required decimal TotalKtmbCost { get; init; }

  public required DepositSummary Deposits { get; init; }

  public required InternalFees InternalFees { get; init; }

  public required GatewayFeeSummary GatewayFees { get; init; }

  // actual-KTMB-cost capture coverage over the range's completed bookings
  public required KtmbActualCoverage KtmbActualCoverage { get; init; }

  public required DirectionBreakdown[] ByDirection { get; init; }
}

public record BookingAnalysis
{
  public required IEnumerable<BookingAnalysisRow> Rows { get; init; }

  public required BookingAnalysisSummary Summary { get; init; }

  public required MonthlyAnalysisRow[] Monthly { get; init; }

  public required PriceComponentSummary[] Components { get; init; }

  public required ComponentsCoverage ComponentsCoverage { get; init; }
}

// The raw range-scoped sums the data layer computes DB-side; the pure
// calculator below turns them into the reported fee figures
public record BookingAnalysisLedgerSums
{
  public required decimal DepositFees { get; init; }

  public required decimal WithdrawalFees { get; init; }

  public required decimal PriorityFeesCharged { get; init; }

  public required decimal PriorityFeesRefunded { get; init; }

  // gross collected for bookings that ended Terminated in the range
  public required decimal TerminatedGross { get; init; }

  // what the BookingTerminated ledger rows returned to wallets in the range
  public required decimal TerminationRefunds { get; init; }
}

// Pure summary math, shared by the repository and the unit tests
public static class BookingAnalysisCalculator
{
  public static BookingAnalysisSummary Summarize(
    IReadOnlyCollection<BookingAnalysisRow> rows,
    DepositSummary deposits,
    BookingAnalysisLedgerSums sums,
    GatewayFeeSummary gatewayFees,
    KtmbActualCoverage ktmbCoverage
  ) =>
    new()
    {
      TotalTickets = rows.Sum(r => r.TicketsCompleted),
      TotalGross = rows.Sum(r => r.GrossRevenue),
      TotalKtmbCost = rows.Sum(r => r.KtmbCost),
      Deposits = deposits,
      InternalFees = new InternalFees
      {
        Deposit = sums.DepositFees,
        Withdrawal = sums.WithdrawalFees,
        Priority = sums.PriorityFeesCharged - sums.PriorityFeesRefunded,
        Termination = sums.TerminatedGross - sums.TerminationRefunds,
      },
      GatewayFees = gatewayFees,
      KtmbActualCoverage = ktmbCoverage,
      ByDirection = ByDirection(rows),
    };

  // per-direction totals over the given rows, stable direction order
  public static DirectionBreakdown[] ByDirection(IEnumerable<BookingAnalysisRow> rows) =>
    rows.GroupBy(r => r.Direction)
      .OrderBy(g => g.Key)
      .Select(g => new DirectionBreakdown
      {
        Direction = g.Key,
        Tickets = g.Sum(r => r.TicketsCompleted),
        Gross = g.Sum(r => r.GrossRevenue),
        KtmbCost = g.Sum(r => r.KtmbCost),
      })
      .ToArray();

  public static string MonthKey(DateOnly date) => date.ToString("MM-yyyy");

  // One row per SGT calendar month that has activity (completed bookings or
  // gateway/internal fee movement). Net = Gross − KtmbCost − gateway fees;
  // internal fees are revenue and excluded from the cost side.
  public static MonthlyAnalysisRow[] MonthlyRollup(
    IReadOnlyCollection<BookingAnalysisRow> rows,
    IReadOnlyDictionary<string, MonthlyExternalSums> external
  )
  {
    var byMonth = rows.GroupBy(r => MonthKey(r.Date)).ToDictionary(g => g.Key, g => g.ToArray());
    var months = byMonth
      .Keys.Concat(external.Keys)
      .Distinct()
      // "MM-yyyy" sorts chronologically on (yyyy, MM)
      .OrderBy(m => m[3..])
      .ThenBy(m => m[..2])
      .ToArray();

    return months
      .Select(m =>
      {
        var monthRows = byMonth.GetValueOrDefault(m, []);
        var ext =
          external.GetValueOrDefault(m)
          ?? new MonthlyExternalSums
          {
            GatewayPaymentFees = 0m,
            GatewayPayoutFees = 0m,
            InternalFees = 0m,
          };
        var gross = monthRows.Sum(r => r.GrossRevenue);
        var ktmb = monthRows.Sum(r => r.KtmbCost);
        return new MonthlyAnalysisRow
        {
          Month = m,
          Gross = gross,
          KtmbCost = ktmb,
          GatewayPaymentFees = ext.GatewayPaymentFees,
          GatewayPayoutFees = ext.GatewayPayoutFees,
          InternalFees = ext.InternalFees,
          Net = gross - ktmb - ext.GatewayPaymentFees - ext.GatewayPayoutFees,
          ByDirection = ByDirection(monthRows),
        };
      })
      .ToArray();
  }
}

// ---- boost ledger (GET Booking/analysis/boosts) ----

public record BookingBoostQuery
{
  public DateOnly? After { get; init; }

  public DateOnly? Before { get; init; }

  public required int Limit { get; init; }

  public required int Skip { get; init; }
}

// one prioritized booking. BoostedAt: PrioritizedAt when stamped (rows from
// this release onward); older boosts fall back to the booking's CreatedAt —
// the best persisted proxy (the prioritize flow historically stamped no
// timestamp). GrantedBy: the caller's sub when someone other than the owner
// (an admin) prioritized the booking — stamped from this release onward,
// null before; a free self-boost via FreeTarget role match stays null.
public record BookingBoost
{
  public required Guid BookingId { get; init; }

  public required string UserId { get; init; }

  // email when known, else username — same identity precedence the admin
  // user lists use
  public required string UserIdentity { get; init; }

  public required DateOnly Date { get; init; }

  public required TimeOnly Time { get; init; }

  public required TrainDirection Direction { get; init; }

  // the fee charged; null when the boost was free (nothing to refund)
  public required decimal? Fee { get; init; }

  public required bool Free { get; init; }

  public required DateTime BoostedAt { get; init; }

  public required string? GrantedBy { get; init; }
}

public record BookingBoostPage
{
  public required int Total { get; init; }

  public required BookingBoost[] Items { get; init; }
}

// ---- travel-date demand view (GET Booking/analysis/travel) ----

// Demand view for the admin Analysis page: how many tickets are secured FOR
// each travel date, per direction, collapsed into quarter-day (6-hour)
// departure buckets — a day is 4 scannable rows instead of every timeslot.
// Unlike BookingAnalysisQuery this ranges over the booking's TRAVEL date
// (b.Date), not CompletedAt.
public record TravelAnalysisQuery
{
  // inclusive TRAVEL-date range; null = unbounded. Travel Date/Time are SGT
  // wall-clock columns already, so no instant conversion is involved.
  public DateOnly? After { get; init; }

  public DateOnly? Before { get; init; }
}

// one scanned timeslot: how many bookings share this travel
// date/direction/departure time at this status — the raw input the
// calculator filters and collapses into quarter-day buckets
public record TravelSlotCount
{
  public required DateOnly Date { get; init; }

  public required TrainDirection Direction { get; init; }

  public required TimeOnly Time { get; init; }

  public required BookStatus Status { get; init; }

  public required int Tickets { get; init; }
}

// one quarter-day demand row: completed tickets travelling on this date, in
// this direction, departing within [QuarterStartHour, QuarterStartHour + 6)
public record TravelAnalysisRow
{
  // the booking's TRAVEL date (SGT wall-clock), NOT its CompletedAt date
  public required DateOnly Date { get; init; }

  public required TrainDirection Direction { get; init; }

  // floor of the departure hour to 6h buckets: 0, 6, 12 or 18
  public required int QuarterStartHour { get; init; }

  public required int Tickets { get; init; }
}

// Pure demand math, shared by the repository and the unit tests. Analyze is
// the full specification — only Completed bookings count, the travel-date
// range is inclusive on both ends, and only buckets with tickets remain —
// while the repository pushes the same filters into SQL so the scan stays
// cheap and reuses this for the bucket/order stage.
public static class TravelAnalysisCalculator
{
  // [0,6) -> 0, [6,12) -> 6, [12,18) -> 12, [18,24) -> 18
  public static int QuarterStartHour(TimeOnly time) => time.Hour / 6 * 6;

  public static TravelAnalysisRow[] Analyze(
    IEnumerable<TravelSlotCount> slots,
    DateOnly? after,
    DateOnly? before
  ) =>
    [
      .. slots
        .Where(s => s.Status == BookStatus.Completed)
        .Where(s => (after is null || s.Date >= after) && (before is null || s.Date <= before))
        .GroupBy(s => (s.Date, s.Direction, Quarter: QuarterStartHour(s.Time)))
        .Select(g => new TravelAnalysisRow
        {
          Date = g.Key.Date,
          Direction = g.Key.Direction,
          QuarterStartHour = g.Key.Quarter,
          Tickets = g.Sum(s => s.Tickets),
        })
        .Where(r => r.Tickets > 0)
        .OrderBy(r => r.Date)
        .ThenBy(r => r.Direction)
        .ThenBy(r => r.QuarterStartHour),
    ];
}

public interface IBookingAnalysisRepository
{
  Task<Result<BookingAnalysis>> Analyze(BookingAnalysisQuery query);

  Task<Result<TravelAnalysisRow[]>> TravelAnalysis(TravelAnalysisQuery query);

  Task<Result<BookingBoostPage>> Boosts(BookingBoostQuery query);
}
