using System.Text.Json;
using App.Modules.Bookings.API.V1;
using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// argon builds its P&L page against this EXACT wire shape — pin the
// serialized JSON so a rename on our side can never silently break it.
// ASP.NET Core serializes with JsonSerializerDefaults.Web (camelCase).
public class PnlTerminalWireContractTests
{
  private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

  private static PnlTerminalRow Row() =>
    new()
    {
      Month = "08-2026",
      Deposits = 1000.5m,
      PaymentFees = 23.45m,
      GwRate = 0.023439m,
      Completed = new PnlTerminalCompleted
      {
        Count = 3,
        Collected = 135.5m,
        KtmbCost = 60.25m,
      },
      Terminated = new PnlTerminalTerminated
      {
        Count = 2,
        Kept = 30.5m,
        KtmbCostNet = 25.75m,
        WithExactRefund = 1,
      },
      Withdrawals = new PnlTerminalWithdrawals
      {
        Count = 4,
        Gross = 400m,
        FeeIncome = 16m,
        PayoutFees = 2.35m,
      },
    };

  [Fact]
  public void Terminal_pnl_row_serializes_with_the_pinned_names_and_nesting()
  {
    var json = JsonSerializer.Serialize(Row().ToRes(), Web);

    json.Should()
      .Be(
        "{\"month\":\"08-2026\",\"deposits\":1000.5,\"paymentFees\":23.45,"
          + "\"gwRate\":0.023439,"
          + "\"completed\":{\"count\":3,\"collected\":135.5,\"ktmbCost\":60.25},"
          + "\"terminated\":{\"count\":2,\"kept\":30.5,\"ktmbCostNet\":25.75,"
          + "\"withExactRefund\":1},"
          + "\"withdrawals\":{\"count\":4,\"gross\":400,\"feeIncome\":16,"
          + "\"payoutFees\":2.35}}"
      );
  }

  [Fact]
  public void Terminal_pnl_rows_serialize_as_a_flat_array()
  {
    var rows = new[] { Row() };

    var json = JsonSerializer.Serialize(rows.Select(r => r.ToRes()), Web);

    json.Should().StartWith("[{\"month\":\"08-2026\",");
    json.Should().EndWith("}]");
  }

  [Fact]
  public void Terminal_pnl_query_binds_optional_after_and_before_as_dd_MM_yyyy()
  {
    var req = new BookingPnlTerminalQueryReq("01-08-2026", null);

    var domain = req.ToDomain();

    domain.After.Should().Be(new DateOnly(2026, 8, 1));
    domain.Before.Should().BeNull();
  }
}
