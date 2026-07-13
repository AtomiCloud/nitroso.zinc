using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// The SGT wall-clock window primitive used by policy rules: half-open
// [start, end), wrap-around midnight supported, null/equal bounds = always
public class PriorityRulesTests
{
  private static readonly TimeOnly Noon = new(12, 0);

  [Fact]
  public void Null_bounds_are_always_open()
  {
    PriorityRules.WindowOpen(null, null, Noon).Should().BeTrue();
    PriorityRules.WindowOpen(new TimeOnly(1, 0), null, Noon).Should().BeTrue();
    PriorityRules.WindowOpen(null, new TimeOnly(1, 0), Noon).Should().BeTrue();
  }

  [Fact]
  public void Equal_bounds_mean_all_day_not_never()
  {
    PriorityRules
      .WindowOpen(new TimeOnly(0, 0), new TimeOnly(0, 0), Noon)
      .Should()
      .BeTrue("00:00 -> 00:00 reads as 'all day', not 'never'");
  }

  [Fact]
  public void Plain_window_is_half_open()
  {
    var start = new TimeOnly(9, 0);
    var end = new TimeOnly(17, 0);
    PriorityRules.WindowOpen(start, end, new TimeOnly(9, 0)).Should().BeTrue("start inclusive");
    PriorityRules.WindowOpen(start, end, Noon).Should().BeTrue();
    PriorityRules.WindowOpen(start, end, new TimeOnly(17, 0)).Should().BeFalse("end exclusive");
    PriorityRules.WindowOpen(start, end, new TimeOnly(3, 0)).Should().BeFalse();
  }

  [Fact]
  public void Wrapping_window_crosses_midnight()
  {
    var start = new TimeOnly(22, 0);
    var end = new TimeOnly(2, 0);
    PriorityRules.WindowOpen(start, end, new TimeOnly(23, 0)).Should().BeTrue();
    PriorityRules.WindowOpen(start, end, new TimeOnly(1, 0)).Should().BeTrue();
    PriorityRules.WindowOpen(start, end, new TimeOnly(2, 0)).Should().BeFalse("end exclusive");
    PriorityRules.WindowOpen(start, end, Noon).Should().BeFalse();
  }
}
