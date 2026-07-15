using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain;
using Domain.Booking;
using Domain.User;
using Domain.Withdrawal;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Users.Data;

// Read-only partner reporting. All source scans aggregate DB-side, with the
// pure domain calculator owning the final inclusive filter, month merge and
// chronological ordering.
public class PartnerEconomicsRepository(
  MainDbContext db,
  ILogger<PartnerEconomicsRepository> logger
) : IPartnerEconomicsRepository
{
  private sealed class PartnerDto
  {
    public string Id { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
  }

  private sealed class PnlDayDto
  {
    public DateOnly Date { get; set; }

    public int Bookings { get; set; }

    public decimal Collected { get; set; }

    public decimal KtmbCost { get; set; }

    public decimal Deposits { get; set; }

    public decimal WithdrawalGross { get; set; }

    public decimal WithdrawalFeeIncome { get; set; }
  }

  public async Task<Result<PartnerUser[]>> ListPartners()
  {
    try
    {
      logger.LogInformation("Listing partner users");
      var partners = await db
        .Database.SqlQuery<PartnerDto>(
          $"""
          SELECT
            u."Id" AS "Id",
            u."Username" AS "Username",
            COALESCE(u."Email", '') AS "Email"
          FROM "Users" u
          WHERE EXISTS (
            SELECT 1
            FROM unnest(u."ExtraRoles") AS r(role)
            WHERE lower(r.role) = 'partner'
          )
          ORDER BY u."Username", u."Id"
          """
        )
        .ToArrayAsync();

      return partners
        .Select(p => new PartnerUser
        {
          Id = p.Id,
          Username = p.Username,
          Email = p.Email,
        })
        .ToArray();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to list partner users");
      return e;
    }
  }

  public async Task<Result<PartnerPnlRow[]>> Pnl(string userId, PartnerPnlQuery query)
  {
    try
    {
      logger.LogInformation("Computing partner P&L for User '{UserId}' with {@Query}", userId, query.ToJson());
      var sgt = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
      var afterUtc =
        query.After?.ToZonedDateTime(TimeOnly.MinValue, sgt)
        ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
      var beforeUtc =
        query.Before?.AddDays(1).ToZonedDateTime(TimeOnly.MinValue, sgt)
        ?? DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
      var completedBooking = (byte)BookStatus.Completed;
      var completedWithdrawal = (byte)WithdrawStatus.Completed;

      // Resolve the user's wallet once, then scope every money source to it.
      // Bookings carry UserId rather than WalletId, so their arm follows the
      // same wallet -> user link before applying the standard request-
      // transaction revenue attribution and per-booking FX LATERAL.
      var days = await db
        .Database.SqlQuery<PnlDayDto>(
          $"""
          WITH wallet AS (
            SELECT w."Id", w."UserId"
            FROM "Wallets" w
            WHERE w."UserId" = {userId}
          )
          SELECT
            CAST((b."CompletedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            CAST(COUNT(*) AS int) AS "Bookings",
            SUM(t."Amount") AS "Collected",
            SUM(
              CASE
                WHEN b."KtmbAmount" IS NOT NULL AND b."KtmbCurrency" = 'SGD'
                  THEN b."KtmbAmount"
                WHEN b."KtmbAmount" IS NOT NULL AND b."KtmbCurrency" = 'MYR'
                  AND fx."Rate" IS NOT NULL
                  THEN b."KtmbAmount" * fx."Rate"
                ELSE 0
              END
            ) AS "KtmbCost",
            CAST(0 AS numeric) AS "Deposits",
            CAST(0 AS numeric) AS "WithdrawalGross",
            CAST(0 AS numeric) AS "WithdrawalFeeIncome"
          FROM "Bookings" b
          JOIN wallet w ON w."UserId" = b."UserId"
          JOIN "Transactions" t ON t."Id" = b."TransactionId"
          LEFT JOIN LATERAL (
            SELECT f."Rate"
            FROM "KtmbFxRates" f
            WHERE f."EffectiveAt" <= b."CompletedAt"
            ORDER BY f."EffectiveAt" DESC, f."CreatedAt" DESC, f."Id" DESC
            LIMIT 1
          ) fx ON TRUE
          WHERE b."Status" = {completedBooking}
            AND b."CompletedAt" IS NOT NULL
            AND b."CompletedAt" >= {afterUtc}
            AND b."CompletedAt" < {beforeUtc}
          GROUP BY 1

          UNION ALL

          SELECT
            CAST((p."CreatedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            CAST(0 AS int) AS "Bookings",
            CAST(0 AS numeric) AS "Collected",
            CAST(0 AS numeric) AS "KtmbCost",
            SUM(p."CapturedAmount") AS "Deposits",
            CAST(0 AS numeric) AS "WithdrawalGross",
            CAST(0 AS numeric) AS "WithdrawalFeeIncome"
          FROM "Payments" p
          JOIN wallet w ON w."Id" = p."WalletId"
          WHERE p."Status" = 'SUCCEEDED'
            AND p."CreatedAt" >= {afterUtc}
            AND p."CreatedAt" < {beforeUtc}
          GROUP BY 1

          UNION ALL

          SELECT
            CAST((x."CompletedAt" AT TIME ZONE 'UTC' + INTERVAL '8 hours') AS date) AS "Date",
            CAST(0 AS int) AS "Bookings",
            CAST(0 AS numeric) AS "Collected",
            CAST(0 AS numeric) AS "KtmbCost",
            CAST(0 AS numeric) AS "Deposits",
            SUM(x."Amount") AS "WithdrawalGross",
            SUM(COALESCE(x."Fee", 0)) AS "WithdrawalFeeIncome"
          FROM "Withdrawals" x
          JOIN wallet w ON w."Id" = x."WalletId"
          WHERE x."Status" = {completedWithdrawal}
            AND x."CompletedAt" IS NOT NULL
            AND x."CompletedAt" >= {afterUtc}
            AND x."CompletedAt" < {beforeUtc}
          GROUP BY 1
          """
        )
        .ToArrayAsync();

      return PartnerPnlCalculator.Analyze(
        days.Select(d => new PartnerPnlDailySum
        {
          Date = d.Date,
          Bookings = d.Bookings,
          Collected = d.Collected,
          KtmbCost = d.KtmbCost,
          Deposits = d.Deposits,
          WithdrawalGross = d.WithdrawalGross,
          WithdrawalFeeIncome = d.WithdrawalFeeIncome,
        }),
        query.After,
        query.Before
      );
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to compute partner P&L for User '{UserId}' with {@Query}",
        userId,
        query.ToJson()
      );
      return e;
    }
  }
}
