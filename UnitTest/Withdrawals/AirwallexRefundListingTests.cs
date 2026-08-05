using System.Net;
using System.Text;
using App.Modules.Payments.Airwallex;
using App.Modules.Withdrawals.Data;
using CSharp_Result;
using Domain.Withdrawal;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Withdrawals;

// The refund LISTING, which the historic reconciliation depends on. Refunds
// issued manually before WithdrawalMethod.CardRefund existed left no refund id
// in zinc, so GetRefundStatus cannot reach them — combing the created-at window
// is the only handle on them.
public class AirwallexRefundListingTests
{
  private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer)
    : HttpMessageHandler
  {
    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken
    )
    {
      this.Requests.Add(request.RequestUri!.PathAndQuery);
      return Task.FromResult(answer(request));
    }
  }

  private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
  {
    public HttpClient CreateClient(string name) =>
      new(handler, disposeHandler: false) { BaseAddress = new Uri("https://gateway.test/") };
  }

  private sealed class StubAuthenticator : IGatewayAuthenticator
  {
    public Task<Result<string>> GetToken() => Task.FromResult("token".ToResult());
  }

  private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
    new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

  private static AirwallexRefundGateway Gateway(StubHandler handler) =>
    new(
      new AirWallexClient(
        new StubFactory(handler),
        new StubAuthenticator(),
        NullLogger<AirWallexClient>.Instance
      )
    );

  private static readonly DateTime From = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime To = new(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc);

  [Fact]
  public async Task The_window_is_sent_as_from_and_to_created_at()
  {
    var handler = new StubHandler(_ => Json("""{"has_more":false,"items":[]}"""));

    var result = await Gateway(handler).ListRefunds(From, To);

    result.IsSuccess().Should().BeTrue();
    var query = handler.Requests.Should().ContainSingle().Subject;
    query.Should().StartWith("/api/v1/pa/refunds?");
    query.Should().Contain("from_created_at=2026-07-10T00%3A00%3A00Z");
    query.Should().Contain("to_created_at=2026-07-15T01%3A00%3A00Z");
    query.Should().Contain("page_num=0");
    query.Should().Contain("page_size=100");
  }

  [Fact]
  public async Task Paging_follows_page_num_until_has_more_is_false()
  {
    var handler = new StubHandler(request =>
      request.RequestUri!.Query.Contains("page_num=0")
        ? Json(
          """
          {"has_more":true,"items":[
            {"id":"rfd_1","payment_intent_id":"int_1","amount":10.5,"status":"SETTLED",
             "created_at":"2026-07-11T03:04:05Z"}
          ]}
          """
        )
        : Json(
          """
          {"has_more":false,"items":[
            {"id":"rfd_2","payment_intent_id":"int_2","amount":7,"status":"RECEIVED",
             "created_at":"2026-07-12T03:04:05Z"}
          ]}
          """
        )
    );

    var result = await Gateway(handler).ListRefunds(From, To);

    result.IsSuccess().Should().BeTrue();
    result.SuccessOrDefault().Select(r => r.Id).Should().Equal("rfd_1", "rfd_2");
    handler.Requests.Should().HaveCount(2);
    handler.Requests[1].Should().Contain("page_num=1");
  }

  [Fact]
  public async Task A_refund_is_mapped_with_its_arn_amount_and_status()
  {
    var handler = new StubHandler(_ =>
      Json(
        """
        {"has_more":false,"items":[
          {"id":"rfd_1","request_id":"req-1","payment_intent_id":"int_1","amount":42.75,
           "currency":"SGD","status":"SETTLED","created_at":"2026-07-11T03:04:05Z",
           "updated_at":"2026-07-11T09:10:11Z",
           "acquirer_reference_number":"12345678901234567890123"}
        ]}
        """
      )
    );

    var result = await Gateway(handler).ListRefunds(From, To);

    var refund = result.SuccessOrDefault().Should().ContainSingle().Subject;
    refund.Id.Should().Be("rfd_1");
    refund.PaymentIntentId.Should().Be("int_1");
    refund.Amount.Should().Be(42.75m);
    refund.Outcome.Should().Be(PayoutOutcome.Settled);
    refund.AcquirerReferenceNumber.Should().Be("12345678901234567890123");
    refund.CreatedAt.Should().Be(new DateTime(2026, 7, 11, 3, 4, 5, DateTimeKind.Utc));
    refund.UpdatedAt.Should().Be(new DateTime(2026, 7, 11, 9, 10, 11, DateTimeKind.Utc));
    refund.RequestId.Should().Be("req-1");
  }

  // A blank ARN is the same fact as an absent one — and only null leaves a
  // stored value alone on the partial-update write path.
  [Fact]
  public async Task A_blank_arn_is_normalized_to_null()
  {
    var handler = new StubHandler(_ =>
      Json(
        """
        {"has_more":false,"items":[
          {"id":"rfd_1","payment_intent_id":"int_1","amount":1,"status":"RECEIVED",
           "acquirer_reference_number":"   "}
        ]}
        """
      )
    );

    var result = await Gateway(handler).ListRefunds(From, To);

    result.SuccessOrDefault().Single().AcquirerReferenceNumber.Should().BeNull();
  }

  [Fact]
  public async Task Statuses_are_classified_like_every_other_refund_path()
  {
    var handler = new StubHandler(_ =>
      Json(
        """
        {"has_more":false,"items":[
          {"id":"rfd_settled","payment_intent_id":"int_1","amount":1,"status":"SETTLED"},
          {"id":"rfd_failed","payment_intent_id":"int_2","amount":1,"status":"FAILED"},
          {"id":"rfd_flight","payment_intent_id":"int_3","amount":1,"status":"ACCEPTED"}
        ]}
        """
      )
    );

    var result = await Gateway(handler).ListRefunds(From, To);

    result.SuccessOrDefault()
      .Select(r => r.Outcome)
      .Should()
      .Equal(PayoutOutcome.Settled, PayoutOutcome.Failed, PayoutOutcome.InFlight);
  }

  // A 404 means "no refunds in this window" — a normal empty answer, same as
  // the financial-transaction listings this is modelled on.
  [Fact]
  public async Task A_404_is_an_empty_window_not_an_error()
  {
    var handler = new StubHandler(_ => Json("{}", HttpStatusCode.NotFound));

    var result = await Gateway(handler).ListRefunds(From, To);

    result.IsSuccess().Should().BeTrue();
    result.SuccessOrDefault().Should().BeEmpty();
  }

  // Anything else must fail loudly: an empty list would read as "no refunds
  // were ever issued", which is exactly the wrong conclusion to draw.
  [Fact]
  public async Task A_server_error_fails_rather_than_reporting_an_empty_window()
  {
    var handler = new StubHandler(_ => Json("boom", HttpStatusCode.InternalServerError));

    var result = await Gateway(handler).ListRefunds(From, To);

    result.IsSuccess().Should().BeFalse();
  }
}
