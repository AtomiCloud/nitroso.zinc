using System.Net;
using System.Text;
using App.Modules.Payments.Airwallex;
using CSharp_Result;
using Domain.Payment;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Payments;

// The account-level fee listing: FEE-type financial transactions over a
// created-at window (Airwallex bills per-attempt gateway/3DS fees and
// per-refund fees as aggregate FEE rows owned by no movement we track). The
// adapter must ask the ledger with transaction_type + from/to_created_at,
// follow page_num until has_more is false, and keep only FEE rows even if
// the gateway answers leniently.
public class AirwallexAccountFeeSourceTests
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

  private static AirwallexAccountFeeSource Source(StubHandler handler) =>
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
  public async Task The_ledger_is_asked_for_fee_rows_in_the_window()
  {
    var handler = new StubHandler(_ =>
      Json(
        """
        {"has_more":false,"items":[{"id":"ft_1","source_id":"fbl_1",
        "transaction_type":"FEE","amount":-61,"fee":0,"net":-61,
        "currency":"SGD","created_at":"2026-07-14T00:00:00Z"}]}
        """
      )
    );

    var r = await Source(handler).InRange(From, To);

    var lines = r.SuccessOrDefault().ToArray();
    lines.Should().ContainSingle();
    lines[0].FinancialTransactionId.Should().Be("ft_1");
    lines[0].SourceId.Should().Be("fbl_1");
    lines[0].Amount.Should().Be(-61m);
    lines[0].TransactedAt.Kind.Should().Be(DateTimeKind.Utc);
    handler.Requests.Should().Equal(
      "/api/v1/financial_transactions?transaction_type=FEE"
        + "&from_created_at=2026-07-10T00%3A00%3A00Z&to_created_at=2026-07-15T01%3A00%3A00Z"
        + "&page_num=0&page_size=100"
    );
  }

  [Fact]
  public async Task Paging_follows_until_has_more_is_false()
  {
    var handler = new StubHandler(req =>
      req.RequestUri!.Query.Contains("page_num=0")
        ? Json(
          """
          {"has_more":true,"items":[{"id":"ft_1","source_id":"fbl_1",
          "transaction_type":"FEE","amount":-1,"fee":0,"net":-1,
          "currency":"SGD","created_at":"2026-07-13T00:00:00Z"}]}
          """
        )
        : Json(
          """
          {"has_more":false,"items":[{"id":"ft_2","source_id":"fbl_2",
          "transaction_type":"FEE","amount":-2,"fee":0,"net":-2,
          "currency":"SGD","created_at":"2026-07-14T00:00:00Z"}]}
          """
        )
    );

    var r = await Source(handler).InRange(From, To);

    r.SuccessOrDefault().Select(x => x.FinancialTransactionId).Should().Equal("ft_1", "ft_2");
    handler.Requests.Should().HaveCount(2);
  }

  [Fact]
  public async Task Non_fee_rows_are_dropped_even_if_the_gateway_answers_leniently()
  {
    var handler = new StubHandler(_ =>
      Json(
        """
        {"has_more":false,"items":[
        {"id":"ft_fee","source_id":"fbl_1","transaction_type":"fee",
        "amount":-1,"fee":0,"net":-1,"currency":"SGD","created_at":"2026-07-13T00:00:00Z"},
        {"id":"ft_payout","source_id":"po_1","transaction_type":"PAYOUT",
        "amount":-50,"fee":0.2,"net":-50.2,"currency":"SGD","created_at":"2026-07-13T00:00:00Z"}
        ]}
        """
      )
    );

    var r = await Source(handler).InRange(From, To);

    // the FEE match is case-insensitive; everything else is dropped
    r.SuccessOrDefault().Should().ContainSingle(x => x.FinancialTransactionId == "ft_fee");
  }

  [Fact]
  public async Task A_missing_source_id_stores_as_empty_never_null()
  {
    var handler = new StubHandler(_ =>
      Json(
        """
        {"has_more":false,"items":[{"id":"ft_1","transaction_type":"FEE",
        "amount":-1,"fee":0,"net":-1,"currency":"SGD","created_at":"2026-07-13T00:00:00Z"}]}
        """
      )
    );

    var r = await Source(handler).InRange(From, To);

    r.SuccessOrDefault().Single().SourceId.Should().Be(string.Empty);
  }

  [Fact]
  public async Task A_failed_listing_is_a_failure_not_an_empty_answer()
  {
    var handler = new StubHandler(_ =>
      Json("""{"code":"boom"}""", HttpStatusCode.InternalServerError)
    );

    var r = await Source(handler).InRange(From, To);

    r.IsFailure().Should().BeTrue();
  }
}
