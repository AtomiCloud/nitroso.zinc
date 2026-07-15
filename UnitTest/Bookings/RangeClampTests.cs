using Domain;
using FluentAssertions;

namespace UnitTest.Bookings;

// The owner-only history gate: non-owners are clamped to NonOwnerFloor
// onward (a clamp, never an error), owners pass through untouched. A range
// that ends before the floor becomes After > Before, which every repository
// and calculator treats as empty.
public class RangeClampTests
{
  private static readonly DateOnly Floor = RangeClamp.NonOwnerFloor;

  [Fact]
  public void The_floor_is_june_2026()
  {
    Floor.Should().Be(new DateOnly(2026, 6, 1));
  }

  [Fact]
  public void Owners_pass_through_untouched()
  {
    var (after, before) = RangeClamp.Clamp(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), true);

    after.Should().Be(new DateOnly(2024, 1, 1));
    before.Should().Be(new DateOnly(2024, 12, 31));
  }

  [Fact]
  public void Owners_keep_unbounded_ranges()
  {
    var (after, before) = RangeClamp.Clamp(null, null, true);

    after.Should().BeNull();
    before.Should().BeNull();
  }

  [Fact]
  public void Non_owner_unbounded_after_becomes_the_floor()
  {
    var (after, before) = RangeClamp.Clamp(null, null, false);

    after.Should().Be(Floor);
    before.Should().BeNull();
  }

  [Fact]
  public void Non_owner_after_before_the_floor_is_raised_to_it()
  {
    var (after, _) = RangeClamp.Clamp(new DateOnly(2025, 1, 1), null, false);

    after.Should().Be(Floor);
  }

  [Fact]
  public void Non_owner_after_on_or_past_the_floor_is_kept()
  {
    var (atFloor, _) = RangeClamp.Clamp(Floor, null, false);
    var (past, _) = RangeClamp.Clamp(new DateOnly(2026, 7, 2), null, false);

    atFloor.Should().Be(Floor);
    past.Should().Be(new DateOnly(2026, 7, 2));
  }

  [Fact]
  public void Non_owner_range_ending_before_the_floor_becomes_empty_not_an_error()
  {
    var (after, before) = RangeClamp.Clamp(
      new DateOnly(2025, 1, 1),
      new DateOnly(2025, 12, 31),
      false
    );

    // Before stays put and After overtakes it — the empty range the repos
    // and calculators naturally answer with no rows
    after.Should().Be(Floor);
    before.Should().Be(new DateOnly(2025, 12, 31));
    (after > before).Should().BeTrue();
  }

  [Fact]
  public void Non_owner_before_is_never_moved()
  {
    var (_, before) = RangeClamp.Clamp(null, new DateOnly(2026, 8, 15), false);

    before.Should().Be(new DateOnly(2026, 8, 15));
  }
}
