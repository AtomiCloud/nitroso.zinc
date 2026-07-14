using System.Text.Json;
using App.Modules.Bookings.API.V1;
using Domain.Booking;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Bookings;

// argon builds its demand view against this EXACT wire shape — pin the
// serialized JSON so a rename on our side can never silently break it.
// ASP.NET Core serializes with JsonSerializerDefaults.Web (camelCase).
public class TravelAnalysisWireContractTests
{
  private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

  [Fact]
  public void Travel_row_serializes_as_date_direction_quarterStartHour_tickets()
  {
    var row = new TravelAnalysisRow
    {
      Date = new DateOnly(2026, 8, 1),
      Direction = TrainDirection.WToJ,
      QuarterStartHour = 6,
      Tickets = 3,
    };

    var json = JsonSerializer.Serialize(row.ToRes(), Web);

    json.Should().Be("{\"date\":\"01-08-2026\",\"direction\":\"WToJ\",\"quarterStartHour\":6,\"tickets\":3}");
  }

  [Fact]
  public void Travel_rows_serialize_as_a_flat_array()
  {
    var rows = new[]
    {
      new TravelAnalysisRow
      {
        Date = new DateOnly(2026, 8, 1),
        Direction = TrainDirection.JToW,
        QuarterStartHour = 0,
        Tickets = 1,
      },
      new TravelAnalysisRow
      {
        Date = new DateOnly(2026, 8, 1),
        Direction = TrainDirection.WToJ,
        QuarterStartHour = 18,
        Tickets = 2,
      },
    };

    var json = JsonSerializer.Serialize(rows.Select(r => r.ToRes()), Web);

    json.Should().Be(
      "[{\"date\":\"01-08-2026\",\"direction\":\"JToW\",\"quarterStartHour\":0,\"tickets\":1},"
        + "{\"date\":\"01-08-2026\",\"direction\":\"WToJ\",\"quarterStartHour\":18,\"tickets\":2}]"
    );
  }

  [Fact]
  public void Travel_query_binds_optional_after_and_before_as_dd_MM_yyyy()
  {
    var req = new BookingTravelAnalysisQueryReq("01-08-2026", null);

    var domain = req.ToDomain();

    domain.After.Should().Be(new DateOnly(2026, 8, 1));
    domain.Before.Should().BeNull();
  }
}
