using App.Modules.Bookings.Data;
using FluentAssertions;

namespace UnitTest.Bookings;

// The queue-position "ahead" predicate must mirror Reserve()'s ordering:
// priority first (earliest boost wins, PrioritizedAt falling back to
// CreatedAt), then oldest CreatedAt, Id tiebreak. Compiled and evaluated in
// memory — the same expression EF translates to SQL.
public class BookingQueueTests
{
  private static readonly DateTime T0 = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

  private static BookingData Row(
    bool priority,
    DateTime createdAt,
    Guid? id = null,
    DateTime? prioritizedAt = null
  ) =>
    new()
    {
      Id = id ?? Guid.NewGuid(),
      CreatedAt = createdAt,
      Priority = priority,
      PrioritizedAt = prioritizedAt,
    };

  private static bool Ahead(BookingData candidate, BookingData reference) =>
    BookingQueue.AheadOf(reference).Compile()(candidate);

  [Fact]
  public void Priority_jumps_ahead_of_non_priority_even_when_created_later()
  {
    var older = Row(priority: false, T0);
    var newerPriority = Row(priority: true, T0.AddHours(5), prioritizedAt: T0.AddHours(6));

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
  }

  [Fact]
  public void Earlier_boost_outranks_earlier_booking_within_priority_group()
  {
    // booked first but boosted later vs booked later but boosted first —
    // the boost time decides
    var bookedFirstBoostedLater = Row(priority: true, T0, prioritizedAt: T0.AddHours(9));
    var bookedLaterBoostedFirst = Row(
      priority: true,
      T0.AddHours(1),
      prioritizedAt: T0.AddHours(2)
    );

    Ahead(bookedLaterBoostedFirst, bookedFirstBoostedLater).Should().BeTrue();
    Ahead(bookedFirstBoostedLater, bookedLaterBoostedFirst).Should().BeFalse();
  }

  [Fact]
  public void Pre_audit_boost_falls_back_to_created_at_as_boost_time()
  {
    // NULL PrioritizedAt (boosted before the audit column existed) ranks as
    // if boosted at booking time — ahead of any boost stamped after it
    var preAudit = Row(priority: true, T0, prioritizedAt: null);
    var stamped = Row(priority: true, T0.AddMinutes(30), prioritizedAt: T0.AddHours(2));

    Ahead(preAudit, stamped).Should().BeTrue("CreatedAt stands in for the missing boost time");
    Ahead(stamped, preAudit).Should().BeFalse();
  }

  [Fact]
  public void Boost_time_ties_break_by_created_at_then_id()
  {
    var boostTime = T0.AddHours(2);
    var bookedEarlier = Row(priority: true, T0, prioritizedAt: boostTime);
    var bookedLater = Row(priority: true, T0.AddMinutes(5), prioritizedAt: boostTime);

    Ahead(bookedEarlier, bookedLater).Should().BeTrue("boost-time ties fall back to age");
    Ahead(bookedLater, bookedEarlier).Should().BeFalse();
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
    var b = Row(priority: true, T0, prioritizedAt: T0.AddHours(1));
    Ahead(b, b).Should().BeFalse();
  }

  [Fact]
  public void Predicate_agrees_with_reserves_ordering()
  {
    // Reserve() sorts: Priority desc, (PrioritizedAt ?? CreatedAt) asc,
    // CreatedAt asc, Id asc — for every pair the predicate must agree with
    // that ordering
    var rows = new[]
    {
      Row(false, T0),
      Row(true, T0.AddHours(2), prioritizedAt: T0.AddHours(8)),
      Row(false, T0.AddHours(1)),
      Row(true, T0.AddHours(3), prioritizedAt: T0.AddHours(4)),
      Row(true, T0.AddMinutes(30)), // pre-audit boost, NULL PrioritizedAt
      Row(false, T0, new Guid("00000000-0000-0000-0000-00000000000a")),
    };

    var sorted = rows
      .OrderByDescending(x => x.Priority)
      .ThenBy(x => x.PrioritizedAt ?? x.CreatedAt)
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
