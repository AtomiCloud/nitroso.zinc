using System.Text.Json;
using App.Modules.Users.API.V1;
using Domain.User;
using FluentAssertions;

namespace UnitTest.Users;

// Argon builds against these exact flat response shapes. Pin web-default
// camelCase JSON so field additions, renames or reordering are visible.
public class PartnerEconomicsWireContractTests
{
  private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

  [Fact]
  public void Partner_user_serializes_as_id_username_email()
  {
    var user = new PartnerUser
    {
      Id = "user-1",
      Username = "bunny",
      Email = "bunny@example.com",
    };

    var json = JsonSerializer.Serialize(user.ToRes(), Web);

    json.Should().Be("{\"id\":\"user-1\",\"username\":\"bunny\",\"email\":\"bunny@example.com\"}");
  }

  [Fact]
  public void Partner_pnl_rows_serialize_with_the_pinned_fields_in_order()
  {
    var rows = new[]
    {
      new PartnerPnlRow
      {
        Month = "08-2026",
        Bookings = 3,
        Collected = 135.5m,
        KtmbCost = 61.2m,
        Deposits = 200m,
        WithdrawalGross = 90m,
        WithdrawalFeeIncome = 4m,
        BoostCount = 2,
        BoostAmount = 9.9m,
        DistinctPassengers = 3,
      },
    };

    var json = JsonSerializer.Serialize(rows.Select(r => r.ToRes()), Web);

    json.Should()
      .Be(
        "[{\"month\":\"08-2026\",\"bookings\":3,\"collected\":135.5,"
          + "\"ktmbCost\":61.2,\"deposits\":200,\"withdrawalGross\":90,"
          + "\"withdrawalFeeIncome\":4,\"boostCount\":2,\"boostAmount\":9.9,"
          + "\"distinctPassengers\":3}]"
      );
  }

  [Fact]
  public void Partner_pnl_query_binds_optional_dates_as_dd_MM_yyyy()
  {
    var req = new PartnerPnlQueryReq("01-08-2026", null);

    var domain = req.ToDomain();

    domain.After.Should().Be(new DateOnly(2026, 8, 1));
    domain.Before.Should().BeNull();
  }

  [Fact]
  public async Task Partner_pnl_query_validator_rejects_non_standard_dates()
  {
    var validator = new PartnerPnlQueryReqValidator();

    var invalid = await validator.ValidateAsync(new PartnerPnlQueryReq("2026-08-01", null));
    var valid = await validator.ValidateAsync(new PartnerPnlQueryReq(null, "31-08-2026"));

    invalid.IsValid.Should().BeFalse();
    valid.IsValid.Should().BeTrue();
  }
}
