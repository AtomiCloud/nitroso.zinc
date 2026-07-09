namespace App.Modules.Bookings.Data;

// One row of the booking_stats materialized view: booking outcome counts at
// the finest grain the dashboards slice on. Keyless; mapped via ToView so EF
// never tries to create or migrate it — the view lives in raw SQL (see
// BookingStatsView).
public class BookingStatData
{
  // travel date — kept in the grain so range/milestone filters push down
  public DateOnly Date { get; set; }

  // 0 = Sunday .. 6 = Saturday (Postgres EXTRACT(DOW), same as C# DayOfWeek)
  public int DayOfWeek { get; set; }

  public TimeOnly Time { get; set; }

  public int Direction { get; set; }

  public bool Priority { get; set; }

  // purchase-to-departure lead time (BookingStats.LeadLadder)
  public string LeadBucket { get; set; } = string.Empty;

  // bookings sharing the slot instance (BookingStats.DemandLadder)
  public string DemandBucket { get; set; } = string.Empty;

  // delivery-to-departure time for completed bookings
  // (BookingStats.DeliveryLadder); '' sentinel = not applicable — the view
  // stores '' instead of NULL so a plain-column unique index can cover every
  // row (REFRESH CONCURRENTLY needs one, and NULLs are distinct in Postgres
  // unique indexes). Mapped back to null at the domain boundary.
  public string DeliveryBucket { get; set; } = string.Empty;

  public int Total { get; set; }

  public int Completed { get; set; }

  public int Refunded { get; set; }

  public int Cancelled { get; set; }

  public int Terminated { get; set; }

  public int Other { get; set; }
}

// The raw SQL for the booking_stats materialized view. The bucket CASE
// expressions are generated from the SAME constant ladders the C# helpers
// use (BookingStats), so SQL and C# cannot drift.
public static class BookingStatsView
{
  public const string Name = "booking_stats";

  // matches Domain.Booking.BookStatus
  private const int Completed = 2;
  private const int Cancelled = 3;
  private const int Refunded = 4;
  private const int Terminated = 5;

  // departure instant in UTC: travel date+time are SGT (UTC+8) wall-clock
  private const string DepartureUtc =
    "((b.\"Date\" + b.\"Time\" - INTERVAL '8 hours') AT TIME ZONE 'UTC')";

  public static string CreateSql()
  {
    var lead = BookingStats.CaseSql(
      "s.\"LeadHours\"",
      BookingStats.LeadLadder,
      BookingStats.LeadOverflow
    );
    var demand = BookingStats.CaseSql(
      "s.\"SlotCount\"",
      BookingStats.DemandLadder,
      BookingStats.DemandOverflow
    );
    var delivery = BookingStats.CaseSql(
      "s.\"DeliveryHours\"",
      BookingStats.DeliveryLadder,
      BookingStats.DeliveryOverflow
    );

    return $"""
      CREATE MATERIALIZED VIEW {Name} AS
      SELECT
        t."Date",
        CAST(EXTRACT(DOW FROM t."Date") AS int) AS "DayOfWeek",
        t."Time",
        t."Direction",
        t."Priority",
        t."LeadBucket",
        t."DemandBucket",
        t."DeliveryBucket",
        CAST(COUNT(*) AS int) AS "Total",
        CAST(COUNT(*) FILTER (WHERE t."Status" = {Completed}) AS int) AS "Completed",
        CAST(COUNT(*) FILTER (WHERE t."Status" = {Refunded}) AS int) AS "Refunded",
        CAST(COUNT(*) FILTER (WHERE t."Status" = {Cancelled}) AS int) AS "Cancelled",
        CAST(COUNT(*) FILTER (WHERE t."Status" = {Terminated}) AS int) AS "Terminated",
        CAST(COUNT(*) FILTER (WHERE t."Status" NOT IN ({Completed}, {Cancelled}, {Refunded}, {Terminated})) AS int) AS "Other"
      FROM (
        SELECT
          s."Date",
          s."Time",
          s."Direction",
          s."Priority",
          s."Status",
          {lead} AS "LeadBucket",
          {demand} AS "DemandBucket",
          CASE
            WHEN s."Status" = {Completed} AND s."CompletedAt" IS NOT NULL
            THEN {delivery}
            ELSE ''
          END AS "DeliveryBucket"
        FROM (
          SELECT
            b."Date",
            b."Time",
            b."Direction",
            b."Priority",
            b."Status",
            b."CompletedAt",
            EXTRACT(EPOCH FROM ({DepartureUtc} - b."CreatedAt")) / 3600.0 AS "LeadHours",
            EXTRACT(EPOCH FROM ({DepartureUtc} - b."CompletedAt")) / 3600.0 AS "DeliveryHours",
            COUNT(*) OVER (PARTITION BY b."Date", b."Time", b."Direction") AS "SlotCount"
          FROM "Bookings" b
        ) s
      ) t
      GROUP BY
        t."Date",
        t."Time",
        t."Direction",
        t."Priority",
        t."LeadBucket",
        t."DemandBucket",
        t."DeliveryBucket"
      WITH DATA;

      -- unique over the full dimension tuple (unique by construction of the
      -- GROUP BY; no column is ever NULL) — required by REFRESH CONCURRENTLY,
      -- which only accepts plain-column, non-partial unique indexes
      CREATE UNIQUE INDEX "IX_{Name}_Dimensions" ON {Name} (
        "Date",
        "Time",
        "Direction",
        "Priority",
        "LeadBucket",
        "DemandBucket",
        "DeliveryBucket"
      );
      """;
  }

  public static string DropSql() => $"DROP MATERIALIZED VIEW IF EXISTS {Name};";
}
