using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// The pure eligibility rules: (allowlisted OR AllowAll) AND the SGT
// availability window (half-open, wrap-around midnight supported)
public class PriorityRulesTests
{
  private static PrioritySettingsRecord Settings(
    bool allowAll = false,
    TimeOnly? start = null,
    TimeOnly? end = null
  ) =>
    new()
    {
      Fee = 10m,
      AllowAll = allowAll,
      WindowStartSgt = start,
      WindowEndSgt = end,
    };

  private static readonly TimeOnly Noon = new(12, 0);

  // ---- who may prioritize ----

  [Fact]
  public void Not_allowlisted_and_not_allow_all_is_ineligible()
  {
    PriorityRules.Eligible(false, Settings(), Noon).Should().BeFalse();
  }

  [Fact]
  public void Allowlisted_user_is_eligible()
  {
    PriorityRules.Eligible(true, Settings(), Noon).Should().BeTrue();
  }

  [Fact]
  public void Allow_all_makes_everyone_eligible()
  {
    PriorityRules.Eligible(false, Settings(allowAll: true), Noon).Should().BeTrue();
  }

  // ---- availability window ----

  [Fact]
  public void No_window_means_always_available()
  {
    PriorityRules.WindowOpen(null, null, new TimeOnly(3, 59)).Should().BeTrue();
  }

  [Fact]
  public void Normal_window_is_half_open()
  {
    var start = new TimeOnly(9, 0);
    var end = new TimeOnly(17, 0);

    PriorityRules.WindowOpen(start, end, new TimeOnly(9, 0)).Should().BeTrue("start inclusive");
    PriorityRules.WindowOpen(start, end, Noon).Should().BeTrue();
    PriorityRules.WindowOpen(start, end, new TimeOnly(17, 0)).Should().BeFalse("end exclusive");
    PriorityRules.WindowOpen(start, end, new TimeOnly(8, 59)).Should().BeFalse();
    PriorityRules.WindowOpen(start, end, new TimeOnly(23, 0)).Should().BeFalse();
  }

  [Fact]
  public void Wrap_around_midnight_window_covers_both_sides()
  {
    var start = new TimeOnly(22, 0);
    var end = new TimeOnly(2, 0);

    PriorityRules.WindowOpen(start, end, new TimeOnly(23, 30)).Should().BeTrue("before midnight");
    PriorityRules.WindowOpen(start, end, new TimeOnly(1, 30)).Should().BeTrue("after midnight");
    PriorityRules.WindowOpen(start, end, new TimeOnly(22, 0)).Should().BeTrue("start inclusive");
    PriorityRules.WindowOpen(start, end, new TimeOnly(2, 0)).Should().BeFalse("end exclusive");
    PriorityRules.WindowOpen(start, end, Noon).Should().BeFalse("the gap is closed");
  }

  [Fact]
  public void Eligibility_combines_allowlist_and_window()
  {
    var inWindow = new TimeOnly(23, 0);
    var outOfWindow = new TimeOnly(12, 0);
    var s = Settings(start: new TimeOnly(22, 0), end: new TimeOnly(2, 0));

    PriorityRules.Eligible(true, s, inWindow).Should().BeTrue();
    PriorityRules.Eligible(true, s, outOfWindow).Should().BeFalse("outside the window");
    PriorityRules.Eligible(false, s, inWindow).Should().BeFalse("not allowlisted");
    PriorityRules
      .Eligible(false, Settings(allowAll: true, start: new TimeOnly(22, 0), end: new TimeOnly(2, 0)), inWindow)
      .Should()
      .BeTrue();
  }

  [Fact]
  public void Defaults_are_fee_ten_allowlist_only_no_window()
  {
    PrioritySettingsRecord.Default.Fee.Should().Be(10m);
    PrioritySettingsRecord.Default.AllowAll.Should().BeFalse();
    PrioritySettingsRecord.Default.WindowStartSgt.Should().BeNull();
    PrioritySettingsRecord.Default.WindowEndSgt.Should().BeNull();
  }
}
