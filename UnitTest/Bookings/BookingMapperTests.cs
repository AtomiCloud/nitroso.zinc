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
}
