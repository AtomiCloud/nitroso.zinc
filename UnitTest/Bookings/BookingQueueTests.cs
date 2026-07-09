using App.Modules.Bookings.Data;
using FluentAssertions;

namespace UnitTest.Bookings;

// The queue-position "ahead" predicate must mirror Reserve()'s ordering:
// priority first, then oldest CreatedAt, Id tiebreak. Compiled and evaluated
// in memory — the same expression EF translates to SQL.
public class BookingQueueTests
{
  private static readonly DateTime T0 = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

  private static BookingData Row(bool priority, DateTime createdAt, Guid? id = null) =>
    new()
    {
      Id = id ?? Guid.NewGuid(),
      CreatedAt = createdAt,
      Priority = priority,
    };

  private static bool Ahead(BookingData candidate, BookingData reference) =>
    BookingQueue.AheadOf(reference).Compile()(candidate);

  [Fact]
  public void Priority_jumps_ahead_of_non_priority_even_when_created_later()
  {
    var older = Row(priority: false, T0);
    var newerPriority = Row(priority: true, T0.AddHours(5));

    Ahead(newerPriority, older).Should().BeTrue("priority outranks age");
    Ahead(older, newerPriority).Should().BeFalse("non-priority never outranks priority");
  }

  [Fact]
  public void Same_priority_orders_by_created_at()
  {
    var older = Row(priority: false, T0);
    var newer = Row(priority: false, T0.AddMinutes(1));

    Ahead(older, newer).Should().BeTrue();
    Ahead(newer, older).Should().BeFalse();

    var olderP = Row(priority: true, T0);
    var newerP = Row(priority: true, T0.AddMinutes(1));

    Ahead(olderP, newerP).Should().BeTrue("priority ties fall back to age");
    Ahead(newerP, olderP).Should().BeFalse();
  }

  [Fact]
  public void Created_at_ties_break_deterministically_by_id()
  {
    var low = Row(priority: false, T0, new Guid("00000000-0000-0000-0000-000000000001"));
    var high = Row(priority: false, T0, new Guid("00000000-0000-0000-0000-000000000002"));

    Ahead(low, high).Should().BeTrue();
    Ahead(high, low).Should().BeFalse();
  }

  [Fact]
  public void A_booking_is_never_ahead_of_itself()
  {
    var b = Row(priority: true, T0);
    Ahead(b, b).Should().BeFalse();
  }

  [Fact]
  public void Predicate_agrees_with_reserves_ordering()
  {
    // Reserve() sorts: Priority desc, CreatedAt asc, Id asc — for every pair
    // the predicate must agree with that ordering
    var rows = new[]
    {
      Row(false, T0),
      Row(true, T0.AddHours(2)),
      Row(false, T0.AddHours(1)),
      Row(true, T0.AddHours(3)),
      Row(false, T0, new Guid("00000000-0000-0000-0000-00000000000a")),
    };

    var sorted = rows
      .OrderByDescending(x => x.Priority)
      .ThenBy(x => x.CreatedAt)
      .ThenBy(x => x.Id)
      .ToArray();

    for (var i = 0; i < sorted.Length; i++)
    {
      var aheadCount = rows.Count(x => Ahead(x, sorted[i]));
      aheadCount.Should().Be(i, $"position {i + 1} must have exactly {i} bookings ahead");
    }
  }
}
