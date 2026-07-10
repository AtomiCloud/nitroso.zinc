using System.Globalization;
using Domain.Booking;

namespace App.Modules.Bookings.Data;

// Single source of truth for every stats bucket ladder. Each ladder is a
// constant table of (inclusive upper bound, label); BOTH the C# helpers and
// the SQL CASE expressions baked into the booking_stats materialized view
// (see BookingStatsView) are generated from these tables, so the two sides
// cannot drift.
public static class BookingStats
{
  // how long before departure the booking was made. Travel date+time are SGT
  // (UTC+8) wall-clock; CreatedAt is UTC. Bookings made after departure
  // (should not happen) land in the tightest bucket.
  public static readonly (double Hours, string Label)[] LeadLadder =
  [
    (6, "6h"),
    (12, "12h"),
    (24, "24h"),
    (48, "2d"),
    (72, "3d"),
    (96, "4d"),
    (24 * 7, "1w"),
    (24 * 14, "2w"),
    (24 * 21, "3w"),
    (24 * 28, "4w"),
    (24 * 31, "1m"),
    (24 * 61, "2m"),
    (24 * 92, "3m"),
    (24 * 183, "6m"),
  ];

  public const string LeadOverflow = "6m+";

  // how many bookings (any status, any priority) share the same
  // date+time+direction slot instance — a proxy for how contested it was
  public static readonly (double Count, string Label)[] DemandLadder =
  [
    (5, "0-5"),
    (10, "5-10"),
    (20, "10-20"),
    (30, "20-30"),
  ];

  public const string DemandOverflow = "30+";

  // for completed bookings: how long before departure the ticket was
  // delivered (CompletedAt, UTC). Deliveries after departure (should not
  // happen) land in the tightest bucket.
  public static readonly (double Hours, string Label)[] DeliveryLadder =
  [
    (1, "1h"),
    (2, "2h"),
    (3, "3h"),
    (4, "4h"),
    (5, "5h"),
    (6, "6h"),
    (12, "12h"),
    (24, "24h"),
    (48, "48h"),
  ];

  public const string DeliveryOverflow = "48h+";

  // boundaries are inclusive on the tight side — exactly 6h before departure
  // is "6h"
  private static string Bucket(
    double value,
    IEnumerable<(double Bound, string Label)> ladder,
    string overflow
  )
  {
    foreach (var (bound, label) in ladder)
      if (value <= bound)
        return label;
    return overflow;
  }

  public static string LeadTimeBucket(DateOnly date, TimeOnly time, DateTime createdAtUtc)
  {
    var departureUtc = BookingPurchaseTiming.DepartureUtc(date, time);
    var lead = departureUtc - createdAtUtc;
    return Bucket(lead.TotalHours, LeadLadder, LeadOverflow);
  }

  public static string DemandBucket(int slotCount) =>
    Bucket(slotCount, DemandLadder, DemandOverflow);

  public static string DeliveryBucket(double hoursBeforeDeparture) =>
    Bucket(hoursBeforeDeparture, DeliveryLadder, DeliveryOverflow);

  // the SQL twin of Bucket(): CASE WHEN {expr} <= bound THEN 'label' ...
  // ELSE 'overflow' END — same <= boundaries, same order
  public static string CaseSql(
    string expr,
    IEnumerable<(double Bound, string Label)> ladder,
    string overflow
  )
  {
    var whens = ladder.Select(l =>
      $"WHEN {expr} <= {l.Bound.ToString(CultureInfo.InvariantCulture)} THEN '{l.Label}'"
    );
    return $"CASE {string.Join(" ", whens)} ELSE '{overflow}' END";
  }

  // lead-time bucket display/sort order for consumers
  public static readonly string[] BucketOrder =
  [
    .. LeadLadder.Select(x => x.Label),
    LeadOverflow,
  ];
}
