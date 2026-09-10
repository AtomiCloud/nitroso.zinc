using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain;
using Domain.Booking;
using Domain.Invoice;
using Domain.Payment;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Invoices.Data;

// Gathers every figure the partner invoice needs for one SGT month, so the
// month no longer has to be assembled by hand. Everything is aggregated
// DB-side in a single UNION ALL scan (no N+1, no per-booking fetches) and
// folded by the pure InvoiceInputCalculator.
//
// GRAIN: booking-sourced arms carry Bookings."Direction" (1 = JToW,
// 2 = WToJ) so the invoice can print per-route; every other source emits
// direction 0. This is the piece the existing analysis endpoints do not
// provide — they report gateway fees, terminations and withdrawals at month
// level only.
//
// AMOUNT SOURCE (authoritative): the booking's REQUEST transaction amount
// (Bookings.TransactionId -> Transactions.Amount), the same source
// BookingAnalysisRepository uses. Surcharges and discounts are already baked
// into it; the priority fee is NOT — it is its own ledger row — so it is
// summed separately and the engine decides whether to charge processing fee
// on it.
//
// TERMINATIONS: this reports what was COLLECTED. The refund share is applied
// by InvoiceInputCalculator.TerminationRefundRate so the policy lives in one
// place rather than being spread into SQL.
//
// BUCKETING: SGT calendar day on the source event — payment CreatedAt,
// booking CompletedAt, gateway-fee TransactedAt, withdrawal CompletedAt.
// Identical to the P&L endpoints so the two reconcile.
public class InvoiceInputRepository(MainDbContext db, ILogger<InvoiceInputRepository> logger)
  : IInvoiceInputRepository
{
  // One daily subtotal from one source. UNION ALL emits sparse rows; the pure
  // calculator merges them.
  private sealed class InvoiceInputDayDto
  {
    public DateOnly Date { get; set; }

    public int Direction { get; set; }

    public decimal Deposits { get; set; }

    public decimal PaymentMethodFees { get; set; }

    public decimal GatewayFees { get; set; }

    public decimal RefundFees { get; set; }

    public int CompletedCount { get; set; }

    public decimal CompletedRevenue { get; set; }

    public int PriorityPaidCount { get; set; }

    public decimal PriorityFee { get; set; }

    public int PriorityFreeCount { get; set; }

    public int TerminatedCount { get; set; }

    public decimal TerminatedCollected { get; set; }

    public int WithdrawalCount { get; set; }

    public decimal WithdrawalTotal { get; set; }

    public decimal WithdrawalFeeIncome { get; set; }

    public int WithdrawalWithFeeCount { get; set; }
  }

  public async Task<Result<InvoiceInputRow>> Gather(InvoiceInputQuery query)
  {
    try
    {
      logger.LogInformation("Gathering invoice inputs with {@Query}", query.ToJson());
      var sgt = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
      var month = new DateOnly(query.Month.Year, query.Month.Month, 1);
      var after = (DateOnly?)month;
      var before = (DateOnly?)month.AddMonths(1);
      var afterUtc = after.ToUtcRangeStart(sgt);
      // exclusive upper bound: first instant of the following month
      var beforeUtc = before.ToUtcRangeStart(sgt);

      var completedBooking = (byte)BookStatus.Completed;
      var terminatedBooking = (byte)BookStatus.Terminated;
      var payment = (byte)GatewayFeeSourceType.Payment;
      var accountFee = (byte)GatewayFeeSourceType.AccountFee;
      var refund = (byte)GatewayFeeSourceType.Refund;
      var completedWithdrawal = (byte)Domain.Withdrawal.WithdrawStatus.Completed;

      var days = await db
        .Database.SqlQuery<InvoiceInputDayDto>(
          $"""
          SELECT
            CAST((p."CreatedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            CAST(0 AS int) AS "Direction",
            SUM(p."CapturedAmount") AS "Deposits",
            CAST(0 AS numeric) AS "PaymentMethodFees",
            CAST(0 AS numeric) AS "GatewayFees",
            CAST(0 AS numeric) AS "RefundFees",
            CAST(0 AS int) AS "CompletedCount",
            CAST(0 AS numeric) AS "CompletedRevenue",
            CAST(0 AS int) AS "PriorityPaidCount",
            CAST(0 AS numeric) AS "PriorityFee",
            CAST(0 AS int) AS "PriorityFreeCount",
            CAST(0 AS int) AS "TerminatedCount",
            CAST(0 AS numeric) AS "TerminatedCollected",
            CAST(0 AS int) AS "WithdrawalCount",
            CAST(0 AS numeric) AS "WithdrawalTotal",
            CAST(0 AS numeric) AS "WithdrawalFeeIncome",
            CAST(0 AS int) AS "WithdrawalWithFeeCount"
          FROM "Payments" p
          WHERE p."Status" = 'SUCCEEDED'
            AND p."CreatedAt" >= {afterUtc}
            AND p."CreatedAt" < {beforeUtc}
          GROUP BY 1

          UNION ALL

          SELECT
            CAST((g."TransactedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            CAST(0 AS int) AS "Direction",
            CAST(0 AS numeric) AS "Deposits",
            COALESCE(SUM(g."Fee") FILTER (WHERE g."SourceType" = {payment}), 0)
              AS "PaymentMethodFees",
            COALESCE(SUM(g."Fee") FILTER (WHERE g."SourceType" = {accountFee}), 0)
              AS "GatewayFees",
            COALESCE(SUM(g."Fee") FILTER (WHERE g."SourceType" = {refund}), 0)
              AS "RefundFees",
            CAST(0 AS int) AS "CompletedCount",
            CAST(0 AS numeric) AS "CompletedRevenue",
            CAST(0 AS int) AS "PriorityPaidCount",
            CAST(0 AS numeric) AS "PriorityFee",
            CAST(0 AS int) AS "PriorityFreeCount",
            CAST(0 AS int) AS "TerminatedCount",
            CAST(0 AS numeric) AS "TerminatedCollected",
            CAST(0 AS int) AS "WithdrawalCount",
            CAST(0 AS numeric) AS "WithdrawalTotal",
            CAST(0 AS numeric) AS "WithdrawalFeeIncome",
            CAST(0 AS int) AS "WithdrawalWithFeeCount"
          FROM "GatewayFees" g
          WHERE g."TransactedAt" >= {afterUtc} AND g."TransactedAt" < {beforeUtc}
          GROUP BY 1

          UNION ALL

          SELECT
            CAST((b."CompletedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            b."Direction" AS "Direction",
            CAST(0 AS numeric) AS "Deposits",
            CAST(0 AS numeric) AS "PaymentMethodFees",
            CAST(0 AS numeric) AS "GatewayFees",
            CAST(0 AS numeric) AS "RefundFees",
            CAST(COUNT(*) AS int) AS "CompletedCount",
            SUM(t."Amount") AS "CompletedRevenue",
            CAST(
              COUNT(*) FILTER (WHERE b."Priority" AND COALESCE(b."PriorityFee", 0) > 0) AS int
            ) AS "PriorityPaidCount",
            COALESCE(
              SUM(CASE WHEN b."Priority" THEN COALESCE(b."PriorityFee", 0) ELSE 0 END), 0
            ) AS "PriorityFee",
            CAST(
              COUNT(*) FILTER (WHERE b."Priority" AND COALESCE(b."PriorityFee", 0) = 0) AS int
            ) AS "PriorityFreeCount",
            CAST(0 AS int) AS "TerminatedCount",
            CAST(0 AS numeric) AS "TerminatedCollected",
            CAST(0 AS int) AS "WithdrawalCount",
            CAST(0 AS numeric) AS "WithdrawalTotal",
            CAST(0 AS numeric) AS "WithdrawalFeeIncome",
            CAST(0 AS int) AS "WithdrawalWithFeeCount"
          FROM "Bookings" b
          JOIN "Transactions" t ON t."Id" = b."TransactionId"
          WHERE b."Status" = {completedBooking}
            AND b."CompletedAt" IS NOT NULL
            AND b."CompletedAt" >= {afterUtc}
            AND b."CompletedAt" < {beforeUtc}
          GROUP BY 1, 2

          UNION ALL

          SELECT
            CAST((b."CompletedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            b."Direction" AS "Direction",
            CAST(0 AS numeric) AS "Deposits",
            CAST(0 AS numeric) AS "PaymentMethodFees",
            CAST(0 AS numeric) AS "GatewayFees",
            CAST(0 AS numeric) AS "RefundFees",
            CAST(0 AS int) AS "CompletedCount",
            CAST(0 AS numeric) AS "CompletedRevenue",
            CAST(0 AS int) AS "PriorityPaidCount",
            CAST(0 AS numeric) AS "PriorityFee",
            CAST(0 AS int) AS "PriorityFreeCount",
            CAST(COUNT(*) AS int) AS "TerminatedCount",
            SUM(t."Amount") AS "TerminatedCollected",
            CAST(0 AS int) AS "WithdrawalCount",
            CAST(0 AS numeric) AS "WithdrawalTotal",
            CAST(0 AS numeric) AS "WithdrawalFeeIncome",
            CAST(0 AS int) AS "WithdrawalWithFeeCount"
          FROM "Bookings" b
          JOIN "Transactions" t ON t."Id" = b."TransactionId"
          WHERE b."Status" = {terminatedBooking}
            AND b."CompletedAt" IS NOT NULL
            AND b."CompletedAt" >= {afterUtc}
            AND b."CompletedAt" < {beforeUtc}
          GROUP BY 1, 2

          UNION ALL

          SELECT
            CAST((w."CompletedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            CAST(0 AS int) AS "Direction",
            CAST(0 AS numeric) AS "Deposits",
            CAST(0 AS numeric) AS "PaymentMethodFees",
            CAST(0 AS numeric) AS "GatewayFees",
            CAST(0 AS numeric) AS "RefundFees",
            CAST(0 AS int) AS "CompletedCount",
            CAST(0 AS numeric) AS "CompletedRevenue",
            CAST(0 AS int) AS "PriorityPaidCount",
            CAST(0 AS numeric) AS "PriorityFee",
            CAST(0 AS int) AS "PriorityFreeCount",
            CAST(0 AS int) AS "TerminatedCount",
            CAST(0 AS numeric) AS "TerminatedCollected",
            CAST(COUNT(*) AS int) AS "WithdrawalCount",
            SUM(w."Amount") AS "WithdrawalTotal",
            SUM(COALESCE(w."Fee", 0)) AS "WithdrawalFeeIncome",
            CAST(COUNT(*) FILTER (WHERE COALESCE(w."Fee", 0) > 0) AS int)
              AS "WithdrawalWithFeeCount"
          FROM "Withdrawals" w
          WHERE w."Status" = {completedWithdrawal}
            AND w."CompletedAt" IS NOT NULL
            AND w."CompletedAt" >= {afterUtc}
            AND w."CompletedAt" < {beforeUtc}
          GROUP BY 1
          """
        )
        .ToArrayAsync();

      var sums = days.Select(d => new InvoiceInputDailySum
      {
        Date = d.Date,
        Direction = d.Direction,
        Deposits = d.Deposits,
        PaymentMethodFees = d.PaymentMethodFees,
        GatewayFees = d.GatewayFees,
        RefundFees = d.RefundFees,
        CompletedCount = d.CompletedCount,
        CompletedRevenue = d.CompletedRevenue,
        PriorityPaidCount = d.PriorityPaidCount,
        PriorityFee = d.PriorityFee,
        PriorityFreeCount = d.PriorityFreeCount,
        TerminatedCount = d.TerminatedCount,
        TerminatedCollected = d.TerminatedCollected,
        WithdrawalCount = d.WithdrawalCount,
        WithdrawalTotal = d.WithdrawalTotal,
        WithdrawalFeeIncome = d.WithdrawalFeeIncome,
        WithdrawalWithFeeCount = d.WithdrawalWithFeeCount,
      });

      return InvoiceInputCalculator.Gather(sums, month);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to gather invoice inputs with {@Query}", query.ToJson());
      throw;
    }
  }
}
