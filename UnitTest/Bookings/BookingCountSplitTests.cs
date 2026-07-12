using App.Modules.Bookings.API.V1;
using Domain.Booking;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Bookings;

// The queue-count split behind GET Booking/counts: TicketsNeeded stays the
// backward-compatible total (old argon reads it) while Priority/Normal carry
// the split for the new /schedules queue badges.
public class BookingCountSplitTests
{
  private static BookingCount Count(int total, int priority) =>
    new()
    {
      Date = new DateOnly(2026, 7, 20),
      Time = new TimeOnly(8, 30),
      Direction = TrainDirection.JToW,
      TicketsNeeded = total,
      Priority = priority,
      Normal = total - priority,
    };

  [Fact]
  public void Split_parts_sum_to_the_total()
  {
    var c = Count(7, 2);

    (c.Priority + c.Normal).Should().Be(c.TicketsNeeded);
  }

  [Fact]
  public void Res_carries_total_and_split_consistently()
  {
    var res = Count(7, 2).ToRes();

    // old argon keeps reading TicketsNeeded; new argon reads the split
    res.TicketsNeeded.Should().Be(7);
    res.Priority.Should().Be(2);
    res.Normal.Should().Be(5);
    res.Date.Should().Be("20-07-2026");
    res.Time.Should().Be("08:30:00");
    res.Direction.Should().Be("JToW");
  }

  [Fact]
  public void All_priority_slot_has_zero_normal()
  {
    var res = Count(3, 3).ToRes();

    res.Priority.Should().Be(3);
    res.Normal.Should().Be(0);
    res.TicketsNeeded.Should().Be(3);
  }

  [Fact]
  public void No_priority_slot_has_zero_priority()
  {
    var res = Count(4, 0).ToRes();

    res.Priority.Should().Be(0);
    res.Normal.Should().Be(4);
  }
}
