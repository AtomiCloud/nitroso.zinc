namespace App.Modules.Bookings.Data;

public static class BookingStats
{
  // how long before departure the booking was made, in the buckets the
  // success-rate dashboard slices on. Travel date+time are SGT (UTC+8)
  // wall-clock; CreatedAt is UTC. Bookings made after departure (should not
  // happen) land in the tightest bucket.
  public static string LeadTimeBucket(DateOnly date, TimeOnly time, DateTime createdAtUtc)
  {
    var departureUtc = date.ToDateTime(time, DateTimeKind.Unspecified).AddHours(-8);
    var lead = departureUtc - createdAtUtc;
    return lead.TotalHours switch
    {
      <= 6 => "6h",
      <= 12 => "12h",
      <= 24 => "24h",
      <= 48 => "2d",
      <= 72 => "3d",
      <= 96 => "4d",
      <= 24 * 7 => "1w",
      <= 24 * 14 => "2w",
      <= 24 * 21 => "3w",
      <= 24 * 28 => "4w",
      <= 24 * 31 => "1m",
      <= 24 * 61 => "2m",
      <= 24 * 92 => "3m",
      <= 24 * 183 => "6m",
      _ => "6m+",
    };
  }

  // bucket display/sort order for consumers
  public static readonly string[] BucketOrder =
  [
    "6h",
    "12h",
    "24h",
    "2d",
    "3d",
    "4d",
    "1w",
    "2w",
    "3w",
    "4w",
    "1m",
    "2m",
    "3m",
    "6m",
    "6m+",
  ];
}
