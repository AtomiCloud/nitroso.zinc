using App.Modules.Timings.Data;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain;
using Domain.Booking;
using Domain.Transaction;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

// Sales/revenue analysis for the admin "Analysis" page. Everything is
// aggregated DB-side (no N+1, no per-booking fetches).
//
// AMOUNT SOURCE (authoritative): the booking's REQUEST transaction amount
// (Bookings.TransactionId -> Transactions.Amount). Generator.CompleteBooking
// copies create.Amount verbatim into the BookingComplete ledger row and
// Complete() collects exactly b.Transaction.Record.Amount from the reserve,
// so the request row, the BookingComplete row and the wallet movement agree
// by construction — and only the request row is FK-linked per booking, which
// makes it the joinable source of truth.
//
// DATE CONVENTION: rows bucket completed bookings on the SGT (UTC+8)
// calendar date of CompletedAt — the same wall-clock convention booking_stats
// uses for travel dates (its DepartureUtc treats Date+Time as SGT). After/
// Before are inclusive SGT dates, resolved to UTC instants for the scan.
public class BookingAnalysisRepository(MainDbContext db, ILogger<BookingAnalysisRepository> logger)
  : IBookingAnalysisRepository
{
  // raw-SQL row shape: grouping by an SGT calendar date derived from a
  // timestamptz is not LINQ-translatable, so the row query is raw (and still
  // fully DB-side); column names must match the SELECT aliases
  private sealed class RowDto
  {
    public DateOnly Date { get; set; }

    public int Direction { get; set; }

    public TimeOnly Time { get; set; }

    public int TicketsCompleted { get; set; }

    public decimal GrossRevenue { get; set; }
  }

  public async Task<Result<BookingAnalysis>> Analyze(BookingAnalysisQuery query)
  {
    try
    {
      logger.LogInformation("Computing booking analysis with {@Query}", query.ToJson());
      var sgt = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
      // inclusive SGT dates -> half-open UTC instant range [after, before)
      var afterUtc =
        query.After?.ToZonedDateTime(TimeOnly.MinValue, sgt)
        ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
      var beforeUtc =
        query.Before?.AddDays(1).ToZonedDateTime(TimeOnly.MinValue, sgt)
        ?? DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

      var completed = (byte)BookStatus.Completed;
      // AT TIME ZONE 'UTC' renders the instant as UTC wall-clock regardless
      // of the session TimeZone; +8h then CAST AS date = the SGT calendar
      // date (same arithmetic booking_stats bakes into its view)
      var rows = await db
        .Database.SqlQuery<RowDto>(
          $"""
          SELECT
            CAST((b."CompletedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            b."Direction" AS "Direction",
            b."Time" AS "Time",
            CAST(COUNT(*) AS int) AS "TicketsCompleted",
            SUM(t."Amount") AS "GrossRevenue"
          FROM "Bookings" b
          JOIN "Transactions" t ON t."Id" = b."TransactionId"
          WHERE b."Status" = {completed}
            AND b."CompletedAt" IS NOT NULL
            AND b."CompletedAt" >= {afterUtc}
            AND b."CompletedAt" < {beforeUtc}
          GROUP BY 1, b."Direction", b."Time"
          ORDER BY 1, b."Direction", b."Time"
          """
        )
        .ToArrayAsync();

      // re-sort client-side: EF may wrap the raw SQL in an outer SELECT,
      // which does not guarantee the subquery's ORDER BY survives
      var analysisRows = rows
        .OrderBy(x => x.Date)
        .ThenBy(x => x.Direction)
        .ThenBy(x => x.Time)
        .Select(x => new BookingAnalysisRow
        {
          Date = x.Date,
          Direction = x.Direction.ToTrainDirection(),
          Time = x.Time,
          TicketsCompleted = x.TicketsCompleted,
          GrossRevenue = x.GrossRevenue,
        })
        .ToArray();

      // Airwallex intents that captured money, created in range (gateway
      // fees are NOT stored anywhere — deliberately gross)
      var capturedInRange = db.Payments.Where(p =>
        p.CapturedAmount > 0 && p.CreatedAt >= afterUtc && p.CreatedAt < beforeUtc
      );
      var depositCount = await capturedInRange.CountAsync();
      var depositCaptured = await capturedInRange.SumAsync(p => (decimal?)p.CapturedAmount);

      // internal-fee ledger rows in range, one grouped scan; PriorityFee
      // charges and refunds share a type and are told apart by the To account
      var feeTypes = new[]
      {
        (short)TransactionType.DepositFee,
        (short)TransactionType.WithdrawFee,
        (short)TransactionType.PriorityFee,
        (short)TransactionType.BookingTerminated,
      };
      var ledger = await db
        .Transactions.Where(x =>
          feeTypes.Contains(x.TransactionType)
          && x.CreatedAt >= afterUtc
          && x.CreatedAt < beforeUtc
        )
        .GroupBy(x => new { x.TransactionType, x.To })
        .Select(g => new
        {
          g.Key.TransactionType,
          g.Key.To,
          Sum = g.Sum(x => x.Amount),
        })
        .ToArrayAsync();

      // termination fee = what the terminated bookings had collected minus
      // what their BookingTerminated ledger rows refunded in the same range
      var terminatedGross = await db
        .Bookings.Where(b =>
          b.Status == (byte)BookStatus.Terminated
          && b.CompletedAt >= afterUtc
          && b.CompletedAt < beforeUtc
        )
        .SumAsync(b => (decimal?)b.Transaction.Amount);

      decimal LedgerSum(TransactionType type, string? to = null) =>
        ledger
          .Where(x => x.TransactionType == (short)type && (to == null || x.To == to))
          .Sum(x => x.Sum);

      var sums = new BookingAnalysisLedgerSums
      {
        DepositFees = LedgerSum(TransactionType.DepositFee),
        WithdrawalFees = LedgerSum(TransactionType.WithdrawFee),
        PriorityFeesCharged = LedgerSum(
          TransactionType.PriorityFee,
          Accounts.PriorityFee.DisplayName
        ),
        PriorityFeesRefunded = LedgerSum(
          TransactionType.PriorityFee,
          Accounts.Usable.DisplayName
        ),
        TerminatedGross = terminatedGross ?? 0m,
        TerminationRefunds = LedgerSum(TransactionType.BookingTerminated),
      };

      return new BookingAnalysis
      {
        Rows = analysisRows,
        Summary = BookingAnalysisCalculator.Summarize(
          analysisRows,
          new DepositSummary
          {
            Count = depositCount,
            Captured = depositCaptured ?? 0m,
          },
          sums
        ),
      };
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to compute booking analysis with {@Query}", query.ToJson());
      return e;
    }
  }
}
