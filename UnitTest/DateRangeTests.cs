using Domain;
using FluentAssertions;

namespace UnitTest;

public class DateRangeTests
{
  private static readonly TimeZoneInfo Singapore = TimeZoneInfo.FindSystemTimeZoneById(
    "Asia/Singapore"
  );

  [Fact]
  public void Inclusive_sgt_dates_convert_to_half_open_utc_bounds()
  {
    var date = new DateOnly(2026, 8, 1);

    ((DateOnly?)date)
      .ToUtcRangeStart(Singapore)
      .Should()
      .Be(new DateTime(2026, 7, 31, 16, 0, 0, DateTimeKind.Utc));
    ((DateOnly?)date)
      .ToUtcRangeEndExclusive(Singapore)
      .Should()
      .Be(new DateTime(2026, 8, 1, 16, 0, 0, DateTimeKind.Utc));
  }

  [Fact]
  public void Null_and_dateonly_extremes_saturate_without_overflow()
  {
    DateOnly? unbounded = null;

    unbounded
      .ToUtcRangeStart(Singapore)
      .Should()
      .Be(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));
    ((DateOnly?)DateOnly.MinValue)
      .ToUtcRangeStart(Singapore)
      .Should()
      .Be(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));
    unbounded
      .ToUtcRangeEndExclusive(Singapore)
      .Should()
      .Be(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc));
    ((DateOnly?)DateOnly.MaxValue)
      .ToUtcRangeEndExclusive(Singapore)
      .Should()
      .Be(new DateTime(9999, 12, 31, 16, 0, 0, DateTimeKind.Utc));
  }
}
