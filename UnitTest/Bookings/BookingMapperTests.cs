using App.Modules.Bookings.API.V1;
using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

public class BookingMapperTests
{
  [Theory]
  [InlineData(BookStatus.Pending, "Pending")]
  [InlineData(BookStatus.Buying, "Buying")]
  [InlineData(BookStatus.Completed, "Completed")]
  [InlineData(BookStatus.Cancelled, "Cancelled")]
  [InlineData(BookStatus.Refunded, "Refunded")]
  [InlineData(BookStatus.Terminated, "Terminated")]
  [InlineData(BookStatus.Recovering, "Recovering")]
  [InlineData(BookStatus.Duplicate, "Duplicate")]
  [InlineData(BookStatus.RequireManualIntervention, "RequireManualIntervention")]
  public void ToRes_maps_every_book_status(BookStatus status, string expected)
  {
    status.ToRes().Should().Be(expected);
  }

  [Fact]
  public void ToRes_covers_all_enum_values()
  {
    foreach (var status in Enum.GetValues<BookStatus>())
    {
      var act = () => status.ToRes();
      act.Should().NotThrow($"BookStatus.{status} must have a ToRes mapping");
    }
  }

  [Fact]
  public void ToBookStatus_round_trips_every_enum_value()
  {
    foreach (var status in Enum.GetValues<BookStatus>())
    {
      status.ToRes().ToBookStatus().Should().Be(status);
    }
  }

  [Fact]
  public void New_recovery_statuses_preserve_stored_values()
  {
    ((int)BookStatus.Recovering).Should().Be(6);
    ((int)BookStatus.Duplicate).Should().Be(7);
    ((int)BookStatus.RequireManualIntervention).Should().Be(8);
  }

  // ---- priority settings targets: API <-> domain round trip ----

  [Fact]
  public void Priority_settings_targets_round_trip_through_req_and_res()
  {
    var req = new SetPrioritySettingsReq(
      Fee: 5m,
      AllowAll: false,
      WindowStartSgt: null,
      WindowEndSgt: null,
      FreeTarget: new App.Modules.Discounts.API.V1.DiscountTargetReq(
        "Any",
        [new App.Modules.Discounts.API.V1.DiscountMatchReq("admin", "Role")]
      ),
      AccessTarget: new App.Modules.Discounts.API.V1.DiscountTargetReq(
        "All",
        [new App.Modules.Discounts.API.V1.DiscountMatchReq("u1", "UserId")]
      )
    );

    var domain = req.ToDomain();
    domain.FreeTarget.Should().NotBeNull();
    domain.FreeTarget!.MatchMode.Should().Be(Domain.Discount.DiscountMatchMode.Any);
    domain.FreeTarget.Matches.Should().ContainSingle(m =>
      m.Type == Domain.Discount.DiscountMatchType.Role && m.Value == "admin"
    );
    domain.AccessTarget!.MatchMode.Should().Be(Domain.Discount.DiscountMatchMode.All);

    var res = domain.ToRes();
    res.FreeTarget!.MatchMode.Should().Be("Any");
    res.FreeTarget.Matches.Should().ContainSingle(m =>
      m.MatchType == "Role" && m.Value == "admin"
    );
    res.AccessTarget!.MatchMode.Should().Be("All");
    res.AccessTarget.Matches.Should().ContainSingle(m =>
      m.MatchType == "UserId" && m.Value == "u1"
    );
  }

  [Fact]
  public void Priority_settings_null_targets_stay_null_in_both_directions()
  {
    var req = new SetPrioritySettingsReq(10m, true, null, null);
    var domain = req.ToDomain();
    domain.FreeTarget.Should().BeNull();
    domain.AccessTarget.Should().BeNull();

    var res = domain.ToRes();
    res.FreeTarget.Should().BeNull();
    res.AccessTarget.Should().BeNull();
  }
}
