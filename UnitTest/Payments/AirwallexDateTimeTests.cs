using App.Modules.Payments.Airwallex;
using App.Utility;

namespace UnitTest.Payments;

// Airwallex sends refund timestamps with a colon-less offset ("+0000"). The
// built-in DateTime reader rejected them, which failed every refund listing on
// a card that had ever been refunded, and with it every card-refund approve,
// requeue and manual completion (fail-closed 503 in production, 2026-10-10).
public class AirwallexDateTimeTests
{
  // Verbatim shape of the production response that failed (ids shortened).
  private const string RefundListBody = """
    {"has_more":false,"items":[{"acquirer_reference_number":null,"amount":70,
    "created_at":"2026-06-22T21:46:40+0000","currency":"SGD","id":"rfd_x",
    "merchant_order_id":null,"metadata":{},"payment_attempt_id":"att_x",
    "payment_intent_id":"int_x","reason":"Requested by customer: someone",
    "request_id":"req_x","status":"SUCCEEDED","updated_at":"2026-06-30T15:04:46+0000"}]}
    """;

  [Fact]
  public void Refund_listing_with_colonless_offsets_deserialises()
  {
    var list = RefundListBody.ToObj<AirwallexRefundListRes>();

    list.Items.Should().ContainSingle();
    var refund = list.Items![0];
    refund.Amount.Should().Be(70m);
    refund.CreatedAt.Should().Be(new DateTime(2026, 6, 22, 21, 46, 40, DateTimeKind.Utc));
    refund.CreatedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    refund.UpdatedAt.Should().Be(new DateTime(2026, 6, 30, 15, 4, 46, DateTimeKind.Utc));
  }

  [Theory]
  [InlineData("2026-06-22T21:46:40+0000", "2026-06-22T21:46:40Z")]
  [InlineData("2026-06-22T21:46:40+00:00", "2026-06-22T21:46:40Z")]
  [InlineData("2026-06-22T21:46:40Z", "2026-06-22T21:46:40Z")]
  [InlineData("2026-06-22T21:46:40.123Z", "2026-06-22T21:46:40.123Z")]
  [InlineData("2026-06-23T05:46:40+0800", "2026-06-22T21:46:40Z")]
  [InlineData("2026-06-22T16:46:40-0500", "2026-06-22T21:46:40Z")]
  public void Every_offset_form_lands_on_the_same_utc_instant(string raw, string expectedUtc)
  {
    var expected = DateTimeOffset.Parse(expectedUtc).UtcDateTime;

    AirwallexDateTime.Parse(raw).Should().Be(expected);
  }

  [Fact]
  public void Null_and_missing_timestamps_stay_null()
  {
    var body = """{"has_more":false,"items":[{"id":"r","request_id":"q","payment_intent_id":"i","amount":1,"status":"SUCCEEDED","created_at":null}]}""";

    var refund = body.ToObj<AirwallexRefundListRes>().Items![0];

    refund.CreatedAt.Should().BeNull();
    refund.UpdatedAt.Should().BeNull();
  }

  [Fact]
  public void Garbage_timestamp_still_fails_loudly()
  {
    var act = () => AirwallexDateTime.Parse("not a date");

    act.Should().Throw<System.Text.Json.JsonException>();
  }

  [Fact]
  public void Financial_transaction_created_at_accepts_colonless_offset()
  {
    var body = """{"has_more":false,"items":[{"id":"t","created_at":"2026-06-02T05:38:33+0000"}]}""";

    var act = () => body.ToObj<AirwallexFinancialTransactionListRes>();

    act.Should().NotThrow();
  }
}
