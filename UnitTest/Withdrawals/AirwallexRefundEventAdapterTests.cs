using App.Modules.Payments.Airwallex;
using FluentAssertions;

namespace UnitTest.Withdrawals;

// Routing of refund.* webhook events: request-id parsing back into
// (withdrawalId, attempt, index) and status classification. Foreign request
// ids (refunds issued by hand on the dashboard) must parse to null so the
// webhook acknowledges instead of crash-looping.
public class AirwallexRefundEventAdapterTests
{
  private static readonly Guid WithdrawalId = Guid.NewGuid();

  private static AirwallexEvent Event(string name, string requestId, string status) =>
    new()
    {
      Name = name,
      Data = new AirwallexEventData
      {
        Object = new AirwallexEventDataObject
        {
          Id = "rfd_123",
          RequestId = requestId,
          Status = status,
        },
      },
    };

  [Theory]
  [InlineData("refund.received")]
  [InlineData("refund.accepted")]
  [InlineData("refund.settled")]
  [InlineData("refund.failed")]
  public void Refund_events_are_recognized(string name)
  {
    AirwallexEventAdapter.IsRefundEvent(Event(name, "x", "RECEIVED")).Should().BeTrue();
  }

  [Theory]
  [InlineData("payment_intent.succeeded")]
  [InlineData("transfer.paid")]
  public void Non_refund_events_are_not_recognized(string name)
  {
    AirwallexEventAdapter.IsRefundEvent(Event(name, "x", "PAID")).Should().BeFalse();
  }

  [Fact]
  public void Our_request_id_parses_into_withdrawal_attempt_and_outcome()
  {
    var adapter = new AirwallexEventAdapter();
    var evt = Event("refund.settled", $"{WithdrawalId}-3-1", "SETTLED");

    var (withdrawalId, requestId, refundId, attempt, outcome) = adapter.ProcessRefundEvent(evt);

    withdrawalId.Should().Be(WithdrawalId);
    requestId.Should().Be($"{WithdrawalId}-3-1");
    refundId.Should().Be("rfd_123");
    attempt.Should().Be(3);
    outcome.Should().Be(TransferOutcome.Settled);
  }

  [Theory]
  [InlineData("SETTLED", TransferOutcome.Settled)]
  [InlineData("FAILED", TransferOutcome.Failed)]
  [InlineData("RECEIVED", TransferOutcome.InFlight)]
  [InlineData("ACCEPTED", TransferOutcome.InFlight)]
  public void Status_classification(string status, TransferOutcome expected)
  {
    var adapter = new AirwallexEventAdapter();
    var evt = Event("refund.settled", $"{WithdrawalId}-1-0", status);

    var (_, _, _, _, outcome) = adapter.ProcessRefundEvent(evt);

    outcome.Should().Be(expected);
  }

  [Theory]
  [InlineData("manual-dashboard-refund")]
  [InlineData("some_random_id")]
  [InlineData("")]
  public void Foreign_request_ids_yield_null_withdrawal(string requestId)
  {
    var adapter = new AirwallexEventAdapter();
    var evt = Event("refund.settled", requestId, "SETTLED");

    var (withdrawalId, _, _, _, _) = adapter.ProcessRefundEvent(evt);

    withdrawalId.Should().BeNull("foreign refunds must be acknowledged and ignored");
  }

  [Fact]
  public void Bare_guid_request_id_is_foreign_for_refunds()
  {
    // a bare guid is a payment-intent-style id, never a refund fragment id
    var adapter = new AirwallexEventAdapter();
    var evt = Event("refund.settled", WithdrawalId.ToString(), "SETTLED");

    var (withdrawalId, _, _, _, _) = adapter.ProcessRefundEvent(evt);

    withdrawalId.Should().BeNull();
  }
}
