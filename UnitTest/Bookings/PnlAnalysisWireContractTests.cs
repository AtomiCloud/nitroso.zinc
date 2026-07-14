using System.Text.Json;
using App.Modules.Bookings.API.V1;
using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// The admin frontend builds against this exact flat monthly shape. Pin the
// web-default camelCase JSON so field renames or derived server-side nets
// cannot silently change the contract.
public class PnlAnalysisWireContractTests
{
  private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

  [Fact]
  public void Pnl_row_serializes_with_the_pinned_fields_in_order()
  {
    var row = new PnlAnalysisRow
    {
      Month = "08-2026",
      Deposits = 200m,
      WithdrawalCount = 2,
      WithdrawalTotal = 90m,
      WithdrawalFeeIncome = 4m,
      GatewayFees = 3.25m,
      TicketRevenue = 135.5m,
      KtmbCost = 61.2m,
    };

    var json = JsonSerializer.Serialize(row.ToRes(), Web);

    json.Should()
      .Be(
        "{\"month\":\"08-2026\",\"deposits\":200,\"withdrawalCount\":2,"
          + "\"withdrawalTotal\":90,\"withdrawalFeeIncome\":4,\"gatewayFees\":3.25,"
          + "\"ticketRevenue\":135.5,\"ktmbCost\":61.2}"
      );
  }

  [Fact]
  public void Pnl_rows_serialize_as_a_flat_array_without_derived_nets()
  {
    var rows = new[]
    {
      new PnlAnalysisRow
      {
        Month = "08-2026",
        Deposits = 0m,
        WithdrawalCount = 0,
        WithdrawalTotal = 0m,
        WithdrawalFeeIncome = 0m,
        GatewayFees = 0m,
        TicketRevenue = 45m,
        KtmbCost = 20m,
      },
    };

    var json = JsonSerializer.Serialize(rows.Select(r => r.ToRes()), Web);

    json.Should()
      .Be(
        "[{\"month\":\"08-2026\",\"deposits\":0,\"withdrawalCount\":0,"
          + "\"withdrawalTotal\":0,\"withdrawalFeeIncome\":0,\"gatewayFees\":0,"
          + "\"ticketRevenue\":45,\"ktmbCost\":20}]"
      );
  }

  [Fact]
  public void Pnl_query_binds_optional_after_and_before_as_dd_MM_yyyy()
  {
    var req = new BookingPnlAnalysisQueryReq("01-08-2026", null);

    var domain = req.ToDomain();

    domain.After.Should().Be(new DateOnly(2026, 8, 1));
    domain.Before.Should().BeNull();
  }
}
