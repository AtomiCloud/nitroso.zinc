using System.Text.Json;
using App.Modules.Bookings.API.V1;
using FluentAssertions;

namespace UnitTest.Bookings;

// argon (PR #281) and tin (PR #44) hand-added these EXACT wire shapes — pin
// the serialized JSON so a rename on our side can never silently break them.
// ASP.NET Core serializes with JsonSerializerDefaults.Web (camelCase).
public class KtmbWireContractTests
{
  private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

  [Fact]
  public void Fx_rate_view_serializes_as_current_and_recent_rows()
  {
    var at = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    var row = new KtmbFxRateRowRes(0.32m, at, at);
    var res = new KtmbFxRateRes(row, [row]);

    var json = JsonSerializer.Serialize(res, Web);

    json.Should().Be(
      "{\"current\":{\"rate\":0.32,\"effectiveAt\":\"2026-07-01T00:00:00Z\",\"createdAt\":\"2026-07-01T00:00:00Z\"},"
        + "\"recent\":[{\"rate\":0.32,\"effectiveAt\":\"2026-07-01T00:00:00Z\",\"createdAt\":\"2026-07-01T00:00:00Z\"}]}"
    );
  }

  [Fact]
  public void Fx_rate_view_serializes_null_current_before_any_row()
  {
    var json = JsonSerializer.Serialize(new KtmbFxRateRes(null, []), Web);

    json.Should().Be("{\"current\":null,\"recent\":[]}");
  }

  [Fact]
  public void Actual_coverage_serializes_as_withActual_and_total()
  {
    var json = JsonSerializer.Serialize(new KtmbActualCoverageRes(3, 9), Web);

    json.Should().Be("{\"withActual\":3,\"total\":9}");
  }

  [Fact]
  public void Missing_worklist_row_serializes_as_id_bookingNo_ticketNo_completedAt()
  {
    var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
    var at = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    var json = JsonSerializer.Serialize(new KtmbCostMissingRes(id, "BN-1", "TN-1", at), Web);

    json.Should().Be(
      "{\"id\":\"11111111-2222-3333-4444-555555555555\",\"bookingNo\":\"BN-1\","
        + "\"ticketNo\":\"TN-1\",\"completedAt\":\"2026-07-01T00:00:00Z\"}"
    );
  }

  [Fact]
  public void Backfill_body_binds_amount_and_currency()
  {
    var req = JsonSerializer.Deserialize<SetBookingKtmbCostReq>(
      "{\"amount\": 35.5, \"currency\": \"MYR\"}",
      Web
    );

    req!.Amount.Should().Be(35.5m);
    req.Currency.Should().Be("MYR");
  }

  [Fact]
  public void Fx_post_body_binds_rate_and_optional_effectiveAt()
  {
    var req = JsonSerializer.Deserialize<SetKtmbFxRateReq>("{\"rate\": 0.32}", Web);

    req!.Rate.Should().Be(0.32m);
    req.EffectiveAt.Should().BeNull();
  }

  [Fact]
  public void Refund_body_binds_refundAmount_and_refundCurrency()
  {
    var req = JsonSerializer.Deserialize<SetBookingKtmbRefundReq>(
      "{\"refundAmount\": 21.3, \"refundCurrency\": \"MYR\"}",
      Web
    );

    req!.RefundAmount.Should().Be(21.3m);
    req.RefundCurrency.Should().Be("MYR");
  }

  [Fact]
  public void Refund_res_serializes_as_id_refundAmount_refundCurrency()
  {
    var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

    var json = JsonSerializer.Serialize(new BookingKtmbRefundRes(id, 21.3m, "MYR"), Web);

    json.Should().Be(
      "{\"id\":\"11111111-2222-3333-4444-555555555555\","
        + "\"refundAmount\":21.3,\"refundCurrency\":\"MYR\"}"
    );
  }
}
