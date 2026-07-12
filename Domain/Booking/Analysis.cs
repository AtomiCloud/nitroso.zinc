using CSharp_Result;
using Domain.Timings;

namespace Domain.Booking;

// Sales/revenue analysis for the admin "Analysis" page. All figures are
// GROSS and internal: Airwallex's own gateway fees are not stored anywhere
// (no API for them yet), so they are deliberately out of scope.
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
// (BookingReserve -> BunnyBooker).
public record BookingAnalysisRow
{
  // SGT date of CompletedAt (matches booking_stats' SGT date convention)
  public required DateOnly Date { get; init; }

  public required TrainDirection Direction { get; init; }

  public required TimeOnly Time { get; init; }

  public required int TicketsCompleted { get; init; }

  public required decimal GrossRevenue { get; init; }
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

public record BookingAnalysisSummary
{
  public required int TotalTickets { get; init; }

  public required decimal TotalGross { get; init; }

  public required DepositSummary Deposits { get; init; }

  public required InternalFees InternalFees { get; init; }
}

public record BookingAnalysis
{
  public required IEnumerable<BookingAnalysisRow> Rows { get; init; }

  public required BookingAnalysisSummary Summary { get; init; }
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
    BookingAnalysisLedgerSums sums
  ) =>
    new()
    {
      TotalTickets = rows.Sum(r => r.TicketsCompleted),
      TotalGross = rows.Sum(r => r.GrossRevenue),
      Deposits = deposits,
      InternalFees = new InternalFees
      {
        Deposit = sums.DepositFees,
        Withdrawal = sums.WithdrawalFees,
        Priority = sums.PriorityFeesCharged - sums.PriorityFeesRefunded,
        Termination = sums.TerminatedGross - sums.TerminationRefunds,
      },
    };
}

public interface IBookingAnalysisRepository
{
  Task<Result<BookingAnalysis>> Analyze(BookingAnalysisQuery query);
}
