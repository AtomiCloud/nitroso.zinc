using System.Net;
using System.Text;
using App.Modules.Payments.Airwallex;
using CSharp_Result;
using Domain.Payment;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Payments;

// Airwallex keys a PAYMENT's financial transactions by the payment ATTEMPT
// id (att_...), not the intent id zinc stores — querying by intent id always
// comes back empty. The fee source must therefore resolve intent ->
// latest_payment_attempt before hitting the fee ledger, while transfers and
// refunds keep querying by their own id, and a missing intent or attempt
// must look like "no rows yet" (stays pending, retried later).
public class AirwallexGatewayFeeSourceTests
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

  private static AirwallexGatewayFeeSource Source(StubHandler handler) =>
    new(
      new AirWallexClient(
        new StubFactory(handler),
        new StubAuthenticator(),
        NullLogger<AirWallexClient>.Instance
      )
    );

  private static PendingFeeSource Pending(string id, GatewayFeeSourceType type) =>
    new() { SourceId = id, SourceType = type };

  private const string IntentWithAttempt = """
    {"id":"int_1","latest_payment_attempt":{"id":"att_9"}}
    """;

  private const string OneFeeRow = """
    {"has_more":false,"items":[{"id":"ft_1","source_id":"att_9","amount":10,
    "fee":0.33,"net":9.67,"currency":"SGD","created_at":"2026-05-02T03:04:05Z"}]}
    """;

  [Fact]
  public async Task Payments_resolve_the_intent_and_query_by_attempt_id()
  {
    var handler = new StubHandler(req =>
      req.RequestUri!.AbsolutePath.Contains("payment_intents")
        ? Json(IntentWithAttempt)
        : Json(OneFeeRow)
    );

    var r = await Source(handler).BySource(Pending("int_1", GatewayFeeSourceType.Payment));

    var lines = r.SuccessOrDefault().ToArray();
    lines.Should().ContainSingle();
    lines[0].FinancialTransactionId.Should().Be("ft_1");
    lines[0].Fee.Should().Be(0.33m);
    lines[0].Net.Should().Be(9.67m);
    lines[0].TransactedAt.Kind.Should().Be(DateTimeKind.Utc);
    handler.Requests.Should().Equal(
      "/api/v1/pa/payment_intents/int_1",
      "/api/v1/financial_transactions?source_id=att_9&page_num=0&page_size=100"
    );
  }

  [Fact]
  public async Task A_payment_intent_the_gateway_does_not_know_behaves_like_no_rows_yet()
  {
    var handler = new StubHandler(_ => Json("""{"code":"not_found"}""", HttpStatusCode.NotFound));

    var r = await Source(handler).BySource(Pending("int_gone", GatewayFeeSourceType.Payment));

    r.SuccessOrDefault().Should().BeEmpty();
    // the fee ledger is never queried without an attempt id
    handler.Requests.Should().Equal("/api/v1/pa/payment_intents/int_gone");
  }

  [Fact]
  public async Task A_payment_intent_without_an_attempt_behaves_like_no_rows_yet()
  {
    var handler = new StubHandler(_ => Json("""{"id":"int_1"}"""));

    var r = await Source(handler).BySource(Pending("int_1", GatewayFeeSourceType.Payment));

    r.SuccessOrDefault().Should().BeEmpty();
    handler.Requests.Should().Equal("/api/v1/pa/payment_intents/int_1");
  }

  [Theory]
  [InlineData(GatewayFeeSourceType.Refund, "rfd_1")]
  [InlineData(GatewayFeeSourceType.Transfer, "trf_1")]
  public async Task Transfers_and_refunds_query_the_ledger_by_their_own_id(
    GatewayFeeSourceType type,
    string sourceId
  )
  {
    var handler = new StubHandler(_ =>
      Json(
        """
        {"has_more":false,"items":[{"id":"ft_r","source_id":"x","amount":-10,
        "fee":0.1,"net":-10.1,"currency":"SGD","created_at":"2026-05-03T00:00:00Z"}]}
        """
      )
    );

    var r = await Source(handler).BySource(Pending(sourceId, type));

    r.SuccessOrDefault().Should().ContainSingle(x => x.FinancialTransactionId == "ft_r");
    handler.Requests.Should().Equal(
      $"/api/v1/financial_transactions?source_id={sourceId}&page_num=0&page_size=100"
    );
  }

  [Fact]
  public async Task A_failed_intent_lookup_is_a_failure_not_an_empty_answer()
  {
    // a 5xx is transient gateway trouble: the sync driver must see a failure
    // (source stays missing) rather than mistake it for "no fees posted yet"
    var handler = new StubHandler(_ =>
      Json("""{"code":"boom"}""", HttpStatusCode.InternalServerError)
    );

    var r = await Source(handler).BySource(Pending("int_1", GatewayFeeSourceType.Payment));

    r.IsFailure().Should().BeTrue();
  }
}
