using App.Modules.Timings.Data;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain;
using Domain.Booking;
using Domain.Payment;
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
// KTMB COST: the ACTUAL amount tin paid KTMB when recorded — SGD as-is, MYR
// × the newest KtmbFxRates row effective at the booking's CompletedAt (the
// CASE mirrors KtmbActualCost.Effective). Bookings without a usable actual
// (none recorded, or MYR with no effective rate) contribute 0 by default —
// actuals are backfilled and KtmbActualCoverage reports the gap; only when
// the query opts into Estimate are they costed at the newest KtmbCosts row
// with EffectiveAt <= their CompletedAt and a matching Direction (the
// LATERAL mirrors KtmbCostSchedule.EffectiveCost); no configured row = 0.
//
// GATEWAY FEES: from the synced GatewayFees rows (Airwallex financial
// transactions), bucketed on the SGT month of TransactedAt. Payment-type
// rows are money-in fees; Transfer + Refund rows are payout fees.
//
// NET (monthly): Net = Gross − KtmbCost − GatewayPaymentFees −
// GatewayPayoutFees. Internal fees are revenue TO BunnyBooker and are
// reported alongside but never subtracted.
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

    public decimal KtmbCost { get; set; }
  }

  // raw-SQL row shape for the profit view: one row per (travel date,
  // direction, departure time) timeslot with its revenue, actual-cost and
  // coverage sums; column names must match the SELECT aliases
  private sealed class ProfitRowDto
  {
    public DateOnly Date { get; set; }

    public int Direction { get; set; }

    public TimeOnly Time { get; set; }

    public int Tickets { get; set; }

    public decimal Revenue { get; set; }

    public decimal Cost { get; set; }

    public int WithActualCost { get; set; }
  }

  // month-bucketed gateway fee sums (payments vs payouts)
  private sealed class GatewayMonthDto
  {
    public DateOnly Month { get; set; }

    public byte SourceType { get; set; }

    public decimal Fee { get; set; }
  }

  // month-bucketed internal-fee ledger sums
  private sealed class LedgerMonthDto
  {
    public DateOnly Month { get; set; }

    public short TransactionType { get; set; }

    public string To { get; set; } = string.Empty;

    public decimal Sum { get; set; }
  }

  // one persisted price-breakdown line, exploded DB-side from the JSON
  private sealed class ComponentDto
  {
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int TimesApplied { get; set; }

    public decimal TotalDelta { get; set; }
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
      var estimate = query.Estimate;
      // AT TIME ZONE 'UTC' renders the instant as UTC wall-clock regardless
      // of the session TimeZone; +8h then CAST AS date = the SGT calendar
      // date (same arithmetic booking_stats bakes into its view).
      // The LATERALs resolve, per booking at its CompletedAt, the effective
      // KTMB cost estimate (mirror of KtmbCostSchedule.EffectiveCost, 0 when
      // none) and the effective MYR -> SGD rate (mirror of
      // KtmbFxRateSchedule.EffectiveRate, NULL when none); the CASE mirrors
      // KtmbActualCost.Effective — actual amount first (SGD as-is, MYR ×
      // rate); bookings without a usable actual fall back to the estimate
      // only when the query opted in, else they contribute 0.
      var rows = await db
        .Database.SqlQuery<RowDto>(
          $"""
          SELECT
            CAST((b."CompletedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            b."Direction" AS "Direction",
            b."Time" AS "Time",
            CAST(COUNT(*) AS int) AS "TicketsCompleted",
            SUM(t."Amount") AS "GrossRevenue",
            SUM(
              CASE
                WHEN b."KtmbAmount" IS NOT NULL AND b."KtmbCurrency" = 'SGD'
                  THEN b."KtmbAmount"
                WHEN b."KtmbAmount" IS NOT NULL AND b."KtmbCurrency" = 'MYR'
                  AND fx."Rate" IS NOT NULL
                  THEN b."KtmbAmount" * fx."Rate"
                WHEN {estimate} THEN COALESCE(kc."Cost", 0)
                ELSE 0
              END
            ) AS "KtmbCost"
          FROM "Bookings" b
          JOIN "Transactions" t ON t."Id" = b."TransactionId"
          LEFT JOIN LATERAL (
            SELECT k."Cost"
            FROM "KtmbCosts" k
            WHERE k."Direction" = b."Direction" AND k."EffectiveAt" <= b."CompletedAt"
            ORDER BY k."EffectiveAt" DESC, k."CreatedAt" DESC, k."Id" DESC
            LIMIT 1
          ) kc ON TRUE
          LEFT JOIN LATERAL (
            SELECT f."Rate"
            FROM "KtmbFxRates" f
            WHERE f."EffectiveAt" <= b."CompletedAt"
            ORDER BY f."EffectiveAt" DESC, f."CreatedAt" DESC, f."Id" DESC
            LIMIT 1
          ) fx ON TRUE
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
          KtmbCost = x.KtmbCost,
        })
        .ToArray();

      // Airwallex intents that captured money, created in range
      var capturedInRange = db.Payments.Where(p =>
        p.CapturedAmount > 0 && p.CreatedAt >= afterUtc && p.CreatedAt < beforeUtc
      );
      var depositCount = await capturedInRange.CountAsync();
      var depositCaptured = await capturedInRange.SumAsync(p => (decimal?)p.CapturedAmount);
      // coverage: how many of those intents already have synced fee rows
      // (gateway fees post with delay — the UI shows sync completeness)
      var paymentsWithFee = await capturedInRange
        .Where(p => db.GatewayFees.Any(f => f.SourceId == p.ExternalReference))
        .CountAsync();

      // actual-KTMB-cost capture coverage: tin records/backfills the paid
      // amount with delay, so the UI shows how many completed bookings in
      // range already carry one (mirrors the gateway-fee coverage)
      var completedInRange = db.Bookings.Where(b =>
        b.Status == completed
        && b.CompletedAt != null
        && b.CompletedAt >= afterUtc
        && b.CompletedAt < beforeUtc
      );
      var ktmbTotal = await completedInRange.CountAsync();
      var ktmbWithActual = await completedInRange.Where(b => b.KtmbAmount != null).CountAsync();

      // the gateway's own fees, bucketed per SGT month of TransactedAt —
      // one grouped scan feeds both the range summary and the monthly rollup
      var gatewayMonths = await db
        .Database.SqlQuery<GatewayMonthDto>(
          $"""
          SELECT
            CAST(date_trunc(
              'month', g."TransactedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours'
            ) AS date) AS "Month",
            g."SourceType" AS "SourceType",
            SUM(g."Fee") AS "Fee"
          FROM "GatewayFees" g
          WHERE g."TransactedAt" >= {afterUtc} AND g."TransactedAt" < {beforeUtc}
          GROUP BY 1, g."SourceType"
          """
        )
        .ToArrayAsync();

      // internal-fee ledger rows per SGT month, one grouped scan;
      // PriorityFee charges and refunds share a type and are told apart by
      // the To account
      var feeTypes = new[]
      {
        (short)TransactionType.DepositFee,
        (short)TransactionType.WithdrawFee,
        (short)TransactionType.PriorityFee,
        (short)TransactionType.BookingTerminated,
      };
      var ledgerMonths = await db
        .Database.SqlQuery<LedgerMonthDto>(
          $"""
          SELECT
            CAST(date_trunc(
              'month', x."CreatedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours'
            ) AS date) AS "Month",
            x."TransactionType" AS "TransactionType",
            x."To" AS "To",
            SUM(x."Amount") AS "Sum"
          FROM "Transactions" x
          WHERE x."TransactionType" = ANY ({feeTypes})
            AND x."CreatedAt" >= {afterUtc}
            AND x."CreatedAt" < {beforeUtc}
          GROUP BY 1, x."TransactionType", x."To"
          """
        )
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
        ledgerMonths
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

      var payment = (byte)GatewayFeeSourceType.Payment;
      var gatewayFees = new GatewayFeeSummary
      {
        Payments = gatewayMonths.Where(x => x.SourceType == payment).Sum(x => x.Fee),
        Payouts = gatewayMonths.Where(x => x.SourceType != payment).Sum(x => x.Fee),
        Coverage = new GatewayFeeCoverage
        {
          PaymentsWithFee = paymentsWithFee,
          PaymentsTotal = depositCount,
        },
      };

      // month-keyed external sums for the rollup. Internal fees per month:
      // deposit + withdrawal + net priority (charged − refunded); the
      // termination component is range-scoped only (it needs the terminated
      // bookings' gross, which has no per-month ledger row) and is reported
      // in the range summary, not the monthly rows.
      var external = gatewayMonths
        .Select(x => BookingAnalysisCalculator.MonthKey(x.Month))
        .Concat(ledgerMonths.Select(x => BookingAnalysisCalculator.MonthKey(x.Month)))
        .Distinct()
        .ToDictionary(
          m => m,
          m =>
          {
            var g = gatewayMonths
              .Where(x => BookingAnalysisCalculator.MonthKey(x.Month) == m)
              .ToArray();
            var l = ledgerMonths
              .Where(x => BookingAnalysisCalculator.MonthKey(x.Month) == m)
              .ToArray();
            decimal MonthLedger(TransactionType type, string? to = null) =>
              l.Where(x => x.TransactionType == (short)type && (to == null || x.To == to))
                .Sum(x => x.Sum);
            return new MonthlyExternalSums
            {
              GatewayPaymentFees = g.Where(x => x.SourceType == payment).Sum(x => x.Fee),
              GatewayPayoutFees = g.Where(x => x.SourceType != payment).Sum(x => x.Fee),
              InternalFees =
                MonthLedger(TransactionType.DepositFee)
                + MonthLedger(TransactionType.WithdrawFee)
                + MonthLedger(TransactionType.PriorityFee, Accounts.PriorityFee.DisplayName)
                - MonthLedger(TransactionType.PriorityFee, Accounts.Usable.DisplayName),
            };
          }
        );

      var (components, coverage) = await this.Components(afterUtc, beforeUtc, completed);

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
          sums,
          gatewayFees,
          new KtmbActualCoverage { WithActual = ktmbWithActual, Total = ktmbTotal }
        ),
        Monthly = BookingAnalysisCalculator.MonthlyRollup(analysisRows, external),
        Components = components,
        ComponentsCoverage = coverage,
      };
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to compute booking analysis with {@Query}", query.ToJson());
      return e;
    }
  }

  // The travel-date demand view: completed tickets per TRAVEL date,
  // direction and quarter-day (6-hour) departure bucket. Travel Date/Time
  // are SGT wall-clock columns, so the inclusive range filter and grouping
  // need no timezone conversion — one grouped scan per timeslot DB-side,
  // collapsed into buckets by the pure calculator (which owns the spec's
  // filters and ordering).
  public async Task<Result<TravelAnalysisRow[]>> TravelAnalysis(TravelAnalysisQuery query)
  {
    try
    {
      logger.LogInformation("Computing travel analysis with {@Query}", query.ToJson());
      var completed = (byte)BookStatus.Completed;
      var bookings = db.Bookings.Where(b => b.Status == completed);
      if (query.After is { } after)
        bookings = bookings.Where(b => b.Date >= after);
      if (query.Before is { } before)
        bookings = bookings.Where(b => b.Date <= before);

      var slots = await bookings
        .GroupBy(b => new
        {
          b.Date,
          b.Direction,
          b.Time,
        })
        .Select(g => new
        {
          g.Key.Date,
          g.Key.Direction,
          g.Key.Time,
          Tickets = g.Count(),
        })
        .ToArrayAsync();

      return TravelAnalysisCalculator.Analyze(
        slots.Select(s => new TravelSlotCount
        {
          Date = s.Date,
          Direction = s.Direction.ToTrainDirection(),
          Time = s.Time,
          Status = BookStatus.Completed,
          Tickets = s.Tickets,
        }),
        query.After,
        query.Before
      );
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to compute travel analysis with {@Query}", query.ToJson());
      return e;
    }
  }

  // The travel-day profit view: completed bookings' revenue and ACTUAL KTMB
  // cost per TRAVEL date and quarter-day (6-hour) departure bucket, both
  // directions combined. Travel Date/Time are SGT wall-clock columns, so the
  // inclusive range filter and grouping need no timezone conversion — one
  // grouped scan per timeslot DB-side (raw SQL only because the FX LATERAL
  // is not LINQ-translatable), collapsed into blocks by the pure calculator
  // (which owns the spec's filters and ordering).
  //
  // REVENUE reuses the analysis' per-booking attribution: the booking's
  // REQUEST transaction amount (Bookings.TransactionId ->
  // Transactions.Amount — see the AMOUNT SOURCE note above). COST sums only
  // ACTUAL KTMB amounts in SGD — SGD as-is, MYR × the newest KtmbFxRates row
  // effective at the booking's CompletedAt (the same LATERAL Analyze uses);
  // bookings without an actual (or MYR with no effective rate) contribute 0,
  // and WithActualCost counts the bookings that do carry an actual so the UI
  // can show coverage. No estimate fallback here, ever.
  public async Task<Result<ProfitAnalysisRow[]>> ProfitAnalysis(ProfitAnalysisQuery query)
  {
    try
    {
      logger.LogInformation("Computing profit analysis with {@Query}", query.ToJson());
      var completed = (byte)BookStatus.Completed;
      var after = query.After ?? DateOnly.MinValue;
      var before = query.Before ?? DateOnly.MaxValue;

      var slots = await db
        .Database.SqlQuery<ProfitRowDto>(
          $"""
          SELECT
            b."Date" AS "Date",
            b."Direction" AS "Direction",
            b."Time" AS "Time",
            CAST(COUNT(*) AS int) AS "Tickets",
            SUM(t."Amount") AS "Revenue",
            SUM(
              CASE
                WHEN b."KtmbAmount" IS NOT NULL AND b."KtmbCurrency" = 'SGD'
                  THEN b."KtmbAmount"
                WHEN b."KtmbAmount" IS NOT NULL AND b."KtmbCurrency" = 'MYR'
                  AND fx."Rate" IS NOT NULL
                  THEN b."KtmbAmount" * fx."Rate"
                ELSE 0
              END
            ) AS "Cost",
            CAST(COUNT(*) FILTER (WHERE b."KtmbAmount" IS NOT NULL) AS int) AS "WithActualCost"
          FROM "Bookings" b
          JOIN "Transactions" t ON t."Id" = b."TransactionId"
          LEFT JOIN LATERAL (
            SELECT f."Rate"
            FROM "KtmbFxRates" f
            WHERE f."EffectiveAt" <= b."CompletedAt"
            ORDER BY f."EffectiveAt" DESC, f."CreatedAt" DESC, f."Id" DESC
            LIMIT 1
          ) fx ON TRUE
          WHERE b."Status" = {completed}
            AND b."Date" >= {after}
            AND b."Date" <= {before}
          GROUP BY b."Date", b."Direction", b."Time"
          """
        )
        .ToArrayAsync();

      return ProfitAnalysisCalculator.Analyze(
        slots.Select(s => new ProfitSlotSum
        {
          Date = s.Date,
          Direction = s.Direction.ToTrainDirection(),
          Time = s.Time,
          Status = BookStatus.Completed,
          Tickets = s.Tickets,
          Revenue = s.Revenue,
          Cost = s.Cost,
          WithActualCost = s.WithActualCost,
        }),
        query.After,
        query.Before
      );
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to compute profit analysis with {@Query}", query.ToJson());
      return e;
    }
  }

  // Revenue by pricing component over completed bookings in range: explode
  // the persisted price-breakdown JSON DB-side and aggregate per (kind,
  // name); priority-boost revenue comes from the PriorityFee column. Only
  // bookings with a stored breakdown contribute — breakdown persistence
  // started with this release, so coverage tells the UI how many completed
  // bookings actually carry one (old rows are excluded, never guessed).
  private async Task<(PriceComponentSummary[], ComponentsCoverage)> Components(
    DateTime afterUtc,
    DateTime beforeUtc,
    byte completed
  )
  {
    var lines = await db
      .Database.SqlQuery<ComponentDto>(
        $"""
        SELECT
          l ->> 'Kind' AS "Kind",
          l ->> 'Name' AS "Name",
          CAST(COUNT(*) AS int) AS "TimesApplied",
          SUM(CAST(l ->> 'Delta' AS numeric)) AS "TotalDelta"
        FROM "Bookings" b
        CROSS JOIN LATERAL jsonb_array_elements(b."PriceBreakdown" -> 'Lines') AS l
        WHERE b."Status" = {completed}
          AND b."CompletedAt" IS NOT NULL
          AND b."CompletedAt" >= {afterUtc}
          AND b."CompletedAt" < {beforeUtc}
          AND b."PriceBreakdown" IS NOT NULL
        GROUP BY 1, 2
        ORDER BY 1, 2
        """
      )
      .ToArrayAsync();

    var completedInRange = db.Bookings.Where(b =>
      b.Status == completed
      && b.CompletedAt != null
      && b.CompletedAt >= afterUtc
      && b.CompletedAt < beforeUtc
    );
    var total = await completedInRange.CountAsync();
    var withBreakdown = await completedInRange.Where(b => b.PriceBreakdown != null).CountAsync();

    // priority boost revenue: the persisted fee snapshots of completed
    // bookings in range (Completed keeps the fee — the queue jump was
    // consumed; Refunded/Cancelled returned it and are excluded with Status)
    var boosted = completedInRange.Where(b => b.Priority && b.PriorityFee > 0);
    var priorityCount = await boosted.CountAsync();
    var prioritySum = await boosted.SumAsync(b => (decimal?)b.PriorityFee);

    var components = lines
      .Select(x => new PriceComponentSummary
      {
        Kind = x.Kind,
        Name = x.Name,
        TimesApplied = x.TimesApplied,
        TotalDelta = x.TotalDelta,
      })
      .ToList();
    if (priorityCount > 0)
      components.Add(
        new PriceComponentSummary
        {
          Kind = "priorityFee",
          Name = "Priority boost",
          TimesApplied = priorityCount,
          TotalDelta = prioritySum ?? 0m,
        }
      );

    return (
      components.ToArray(),
      new ComponentsCoverage { WithBreakdown = withBreakdown, Total = total }
    );
  }

  // The boost ledger: prioritized bookings whose boost instant is in range,
  // newest first. BoostedAt = PrioritizedAt when stamped (rows from this
  // release onward); older boosts fall back to CreatedAt — the best persisted
  // proxy, since the prioritize flow historically stamped no timestamp.
  public async Task<Result<BookingBoostPage>> Boosts(BookingBoostQuery query)
  {
    try
    {
      logger.LogInformation("Listing booking boosts with {@Query}", query.ToJson());
      var sgt = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
      var afterUtc =
        query.After?.ToZonedDateTime(TimeOnly.MinValue, sgt)
        ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
      var beforeUtc =
        query.Before?.AddDays(1).ToZonedDateTime(TimeOnly.MinValue, sgt)
        ?? DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

      var boosts = db.Bookings.Where(b =>
        b.Priority
        && (b.PrioritizedAt ?? b.CreatedAt) >= afterUtc
        && (b.PrioritizedAt ?? b.CreatedAt) < beforeUtc
      );

      var total = await boosts.CountAsync();
      var items = await boosts
        .OrderByDescending(b => b.PrioritizedAt ?? b.CreatedAt)
        .ThenBy(b => b.Id)
        .Skip(query.Skip)
        .Take(query.Limit)
        .Select(b => new
        {
          b.Id,
          b.UserId,
          // email when known, else username — the same identity precedence
          // admin-facing user lists expose
          Identity = b.User.Email ?? b.User.Username,
          b.Date,
          b.Time,
          b.Direction,
          b.PriorityFee,
          b.PrioritizedAt,
          b.CreatedAt,
          b.PrioritizedBy,
        })
        .ToArrayAsync();

      return new BookingBoostPage
      {
        Total = total,
        Items = items
          .Select(x => new BookingBoost
          {
            BookingId = x.Id,
            UserId = x.UserId,
            UserIdentity = x.Identity,
            Date = x.Date,
            Time = x.Time,
            Direction = x.Direction.ToTrainDirection(),
            Fee = x.PriorityFee,
            Free = x.PriorityFee is null or 0m,
            BoostedAt = x.PrioritizedAt ?? x.CreatedAt,
            GrantedBy = x.PrioritizedBy,
          })
          .ToArray(),
      };
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to list booking boosts with {@Query}", query.ToJson());
      return e;
    }
  }
}
