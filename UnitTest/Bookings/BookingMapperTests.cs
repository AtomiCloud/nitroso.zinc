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

  // ---- priority policies: API <-> domain round trip ----

  [Fact]
  public void Priority_policies_round_trip_through_req_and_res()
  {
    var req = new SetPrioritySettingsReq(
      [
        new PriorityPolicyReq(
          Name: "vip anytime",
          Allow: true,
          Target: new App.Modules.Discounts.API.V1.DiscountTargetReq(
            "Any",
            [new App.Modules.Discounts.API.V1.DiscountMatchReq("vip", "Role")]
          ),
          WindowStartSgt: "14:00:00",
          WindowEndSgt: "16:00:00",
          MinHoursToDeparture: 6m,
          MaxHoursToDeparture: 48m,
          FeeKind: "Percent",
          FeeValue: 12.5m,
          SlotCap: 3
        ),
        new PriorityPolicyReq(Name: "deny rest", Allow: false),
      ]
    );

    var domain = req.ToDomain();
    domain.Policies.Should().HaveCount(2);
    var vip = domain.Policies[0];
    vip.Allow.Should().BeTrue();
    vip.Target!.MatchMode.Should().Be(Domain.Discount.DiscountMatchMode.Any);
    vip.Target.Matches.Should().ContainSingle(m =>
      m.Type == Domain.Discount.DiscountMatchType.Role && m.Value == "vip"
    );
    vip.WindowStartSgt.Should().Be(new TimeOnly(14, 0));
    vip.WindowEndSgt.Should().Be(new TimeOnly(16, 0));
    vip.MinHoursToDeparture.Should().Be(6m);
    vip.MaxHoursToDeparture.Should().Be(48m);
    vip.FeeKind.Should().Be(PriorityFeeKind.Percent);
    vip.FeeValue.Should().Be(12.5m);
    vip.SlotCap.Should().Be(3);
    domain.Policies[1].Allow.Should().BeFalse();
    domain.Policies[1].Target.Should().BeNull();

    var res = domain.ToRes();
    res.Policies.Should().HaveCount(2);
    res.Policies[0].FeeKind.Should().Be("Percent");
    res.Policies[0].WindowStartSgt.Should().Be("14:00:00");
    res.Policies[0].Target!.MatchMode.Should().Be("Any");
    res.Policies[0].SlotCap.Should().Be(3);
    res.Policies[1].Allow.Should().BeFalse();
  }

  [Fact]
  public void Priority_policies_empty_list_round_trips_empty()
  {
    var req = new SetPrioritySettingsReq([]);
    var domain = req.ToDomain();
    domain.Policies.Should().BeEmpty();
    domain.ToRes().Policies.Should().BeEmpty();
  }
}
