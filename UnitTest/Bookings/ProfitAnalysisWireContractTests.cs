using System.Text.Json;
using App.Modules.Bookings.API.V1;
using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// argon builds its profit view against this EXACT wire shape — pin the
// serialized JSON so a rename on our side can never silently break it.
// ASP.NET Core serializes with JsonSerializerDefaults.Web (camelCase).
public class ProfitAnalysisWireContractTests
{
  private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

  [Fact]
  public void Profit_row_serializes_as_date_quarterStartHour_tickets_revenue_cost_withActualCost()
  {
    var row = new ProfitAnalysisRow
    {
      Date = new DateOnly(2026, 8, 1),
      QuarterStartHour = 6,
      Tickets = 3,
      Revenue = 135.5m,
      Cost = 60.25m,
      WithActualCost = 2,
    };

    var json = JsonSerializer.Serialize(row.ToRes(), Web);

    json.Should()
      .Be(
        "{\"date\":\"01-08-2026\",\"quarterStartHour\":6,\"tickets\":3,"
          + "\"revenue\":135.5,\"cost\":60.25,\"withActualCost\":2}"
      );
  }

  [Fact]
  public void Profit_rows_serialize_as_a_flat_array()
  {
    var rows = new[]
    {
      new ProfitAnalysisRow
      {
        Date = new DateOnly(2026, 8, 1),
        QuarterStartHour = 0,
        Tickets = 1,
        Revenue = 45m,
        Cost = 0m,
        WithActualCost = 0,
      },
      new ProfitAnalysisRow
      {
        Date = new DateOnly(2026, 8, 1),
        QuarterStartHour = 18,
        Tickets = 2,
        Revenue = 90m,
        Cost = 41.8m,
        WithActualCost = 2,
      },
    };

    var json = JsonSerializer.Serialize(rows.Select(r => r.ToRes()), Web);

    json.Should()
      .Be(
        "[{\"date\":\"01-08-2026\",\"quarterStartHour\":0,\"tickets\":1,"
          + "\"revenue\":45,\"cost\":0,\"withActualCost\":0},"
          + "{\"date\":\"01-08-2026\",\"quarterStartHour\":18,\"tickets\":2,"
          + "\"revenue\":90,\"cost\":41.8,\"withActualCost\":2}]"
      );
  }

  [Fact]
  public void Profit_query_binds_optional_after_and_before_as_dd_MM_yyyy()
  {
    var req = new BookingProfitAnalysisQueryReq("01-08-2026", null);

    var domain = req.ToDomain();

    domain.After.Should().Be(new DateOnly(2026, 8, 1));
    domain.Before.Should().BeNull();
  }
}
