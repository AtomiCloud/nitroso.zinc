using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using App.Modules.Withdrawals.API.V1;
using App.StartUp.Options;
using App.StartUp.Registry;
using App.StartUp.Services;
using App.StartUp.Services.Auth;
using CSharp_Result;
using Domain.User;
using Domain.Wallet;
using Domain.Withdrawal;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTest.Withdrawals;

public class WithdrawalExporterTests
{
  [Fact]
  public void Positive_max_is_accepted_by_both_withdrawal_query_validators()
  {
    var search = new SearchWithdrawalQuery(
      null,
      null,
      null,
      null,
      100m,
      null,
      null,
      null,
      null,
      null
    );
    var export = new ExportWithdrawalQuery(null, null, null, null, 100m, null, null, null);

    new SearchWithdrawalQueryValidator().Validate(search).IsValid.Should().BeTrue();
    new ExportWithdrawalQueryValidator().Validate(export).IsValid.Should().BeTrue();
  }

  [Fact]
  public async Task Prepare_and_write_page_through_every_matching_withdrawal_without_losing_filters()
  {
    var withdrawals = Enumerable.Range(0, 205).Select(index => MakeWithdrawal(index)).ToArray();
    var repository = new FakeExportRepository(withdrawals);
    var exporter = new WithdrawalExporter(repository, new FakeStorage());
    var search = new WithdrawalSearch
    {
      Id = null,
      UserId = "accountant-user",
      CompleterId = "admin-user",
      Min = 12.34m,
      Max = 567.89m,
      Status = WithdrawStatus.Completed,
      Before = new DateOnly(2024, 12, 31),
      After = new DateOnly(2024, 1, 1),
      Limit = 1,
      Skip = 99,
    };

    var prepared = await exporter.Prepare(search);
    prepared.IsSuccess().Should().BeTrue();
    using var writer = new StringWriter();
    var written = await exporter.Write(prepared.SuccessOrDefault(), writer);

    written.IsSuccess().Should().BeTrue();
    var records = writer
      .ToString()
      .Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries);
    var ids = records.Skip(1).Select(record => record.Split(',')[0]).ToArray();

    records.Should().HaveCount(206, "the header plus every one of the 205 matching withdrawals");
    ids.Should().HaveCount(205).And.OnlyHaveUniqueItems();
    ids.Should().Equal(withdrawals.Select(w => w.Principal.Id.ToString()));
    repository.IssuedSearches.Should().HaveCount(3);
    repository.IssuedSearches[0].Cursor.Should().BeNull();
    repository.IssuedSearches[1].Cursor.Should().Be(CursorFor(withdrawals[99]));
    repository.IssuedSearches[2].Cursor.Should().Be(CursorFor(withdrawals[199]));
    repository.IssuedSearches.Should().OnlyContain(x => x.Limit == WithdrawalExporter.PageSize);
    repository.IssuedSearches.Should().OnlyContain(
      x =>
        x.Search.UserId == search.UserId
        && x.Search.CompleterId == search.CompleterId
        && x.Search.Min == search.Min
        && x.Search.Max == search.Max
        && x.Search.Status == search.Status
        && x.Search.Before == search.Before
        && x.Search.After == search.After
    );
  }

  [Fact]
  public async Task Write_emits_one_bom_exact_header_and_crlf_terminated_records()
  {
    var repository = new FakeExportRepository(
      Enumerable.Range(0, 205).Select(index => MakeWithdrawal(index))
    );
    var exporter = new WithdrawalExporter(repository, new FakeStorage());
    var prepared = await exporter.Prepare(new WithdrawalSearch());
    using var writer = new StringWriter();

    var written = await exporter.Write(prepared.SuccessOrDefault(), writer);
    var csv = writer.ToString();
    var records = csv.Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries);

    written.IsSuccess().Should().BeTrue();
    csv.Should().StartWith("\uFEFF" + WithdrawalCsv.HeaderLine + WithdrawalCsv.LineEnding);
    csv.Count(c => c == '\uFEFF').Should().Be(1);
    csv.Replace(WithdrawalCsv.LineEnding, "").Should().NotContain("\n").And.NotContain("\r");
    records.Should().HaveCount(206);
    records[0].Should().Be("\uFEFF" + WithdrawalCsv.HeaderLine);
  }

  [Fact]
  public async Task Prepare_presigns_first_page_receipts_and_write_emits_the_urls()
  {
    var withdrawal = MakeWithdrawal(1, receipt: "receipts/withdrawal-1.pdf");
    var storage = new FakeStorage();
    var exporter = new WithdrawalExporter(new FakeExportRepository([withdrawal]), storage);

    var prepared = await exporter.Prepare(new WithdrawalSearch());
    using var writer = new StringWriter();
    var written = await exporter.Write(prepared.SuccessOrDefault(), writer);

    prepared.IsSuccess().Should().BeTrue();
    storage.RequestedKeys.Should().Equal("receipts/withdrawal-1.pdf");
    storage.RequestedExpiries.Should().OnlyContain(
      expiry => expiry == TimeSpan.FromDays(7),
      "the export presigns receipts for the 7-day S3/MinIO maximum, not the interactive 1-hour link"
    );
    written.IsSuccess().Should().BeTrue();
    writer.ToString().Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries)[1]
      .Split(',')[16]
      .Should()
      .Be("https://storage.test/receipts/withdrawal-1.pdf");
  }

  [Fact]
  public async Task Export_renders_range_start_boundary_timestamps_in_singapore_time()
  {
    var createdAt = new DateTime(2023, 12, 31, 16, 0, 0, DateTimeKind.Utc);
    var completedAt = new DateTime(2023, 12, 31, 16, 30, 0, DateTimeKind.Utc);
    var withdrawal = MakeWithdrawal(
      1,
      receipt: "receipts/boundary.pdf",
      createdAt: createdAt,
      completedAt: completedAt
    );
    var repository = new FakeExportRepository([withdrawal]);
    var exporter = new WithdrawalExporter(repository, new FakeStorage());
    var search = new WithdrawalSearch { After = new DateOnly(2024, 1, 1) };

    var prepared = await exporter.Prepare(search);
    using var writer = new StringWriter();
    var written = await exporter.Write(prepared.SuccessOrDefault(), writer);
    var fields = writer
      .ToString()
      .Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries)[1]
      .Split(',');

    written.IsSuccess().Should().BeTrue();
    repository.IssuedSearches[0].Search.After.Should().Be(new DateOnly(2024, 1, 1));
    fields[1].Should().Be("2024-01-01T00:00:00+08:00");
    fields[2].Should().Be("2024-01-01T00:30:00+08:00");
  }

  [Fact]
  public async Task Export_renders_the_final_second_of_the_range_start_day_in_singapore_time()
  {
    var withdrawal = MakeWithdrawal(
      1,
      createdAt: new DateTime(2024, 1, 1, 15, 59, 59, DateTimeKind.Utc)
    );
    var repository = new FakeExportRepository([withdrawal]);
    var exporter = new WithdrawalExporter(repository, new FakeStorage());
    var search = new WithdrawalSearch { After = new DateOnly(2024, 1, 1) };

    var prepared = await exporter.Prepare(search);
    using var writer = new StringWriter();
    var written = await exporter.Write(prepared.SuccessOrDefault(), writer);
    var fields = writer
      .ToString()
      .Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries)[1]
      .Split(',');

    written.IsSuccess().Should().BeTrue();
    repository.IssuedSearches[0].Search.After.Should().Be(new DateOnly(2024, 1, 1));
    fields[1].Should().Be("2024-01-01T23:59:59+08:00");
  }

  [Fact]
  public async Task Repository_failure_after_a_written_page_propagates_instead_of_reporting_clean_success()
  {
    var withdrawals = Enumerable.Range(0, 200).Select(index => MakeWithdrawal(index)).ToArray();
    var repository = new FakeExportRepository(withdrawals)
    {
      FailAtCursor = CursorFor(withdrawals[99]),
    };
    var exporter = new WithdrawalExporter(repository, new FakeStorage());
    var prepared = await exporter.Prepare(new WithdrawalSearch());
    using var writer = new StringWriter();

    var written = await exporter.Write(prepared.SuccessOrDefault(), writer);

    written.IsFailure().Should().BeTrue();
    writer
      .ToString()
      .Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries)
      .Should()
      .HaveCount(101, "the controller must abort this partial response instead of presenting success");
  }

  [Fact]
  public async Task Write_propagates_cancellation_between_full_pages_instead_of_reporting_success()
  {
    var withdrawals = Enumerable.Range(0, 205).Select(index => MakeWithdrawal(index)).ToArray();
    var repository = new FakeExportRepository(withdrawals);
    var exporter = new WithdrawalExporter(repository, new FakeStorage());
    var prepared = await exporter.Prepare(new WithdrawalSearch());
    using var cancellation = new CancellationTokenSource();
    using var writer = new CancellingStringWriter(cancellation, lineEndingLimit: 101);

    var write = async () =>
      await exporter.Write(prepared.SuccessOrDefault(), writer, cancellation.Token);

    await write.Should().ThrowAsync<OperationCanceledException>();
    writer
      .ToString()
      .Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries)
      .Should()
      .HaveCount(101, "the header and first page were written before cancellation");
    repository.IssuedSearches.Should().ContainSingle("the next page must not be fetched");
  }

  [Fact]
  public async Task Keyset_paging_does_not_skip_rows_when_a_written_row_is_removed_between_pages()
  {
    var withdrawals = Enumerable.Range(0, 205).Select(index => MakeWithdrawal(index)).ToArray();
    var repository = new FakeExportRepository(withdrawals);
    repository.AfterFirstPage = fake => fake.Remove(withdrawals[50].Principal.Id);
    var exporter = new WithdrawalExporter(repository, new FakeStorage());

    var prepared = await exporter.Prepare(new WithdrawalSearch());
    using var writer = new StringWriter();
    var written = await exporter.Write(prepared.SuccessOrDefault(), writer);
    var ids = writer
      .ToString()
      .Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries)
      .Skip(1)
      .Select(record => record.Split(',')[0]);

    written.IsSuccess().Should().BeTrue();
    ids.Should().Equal(withdrawals.Select(x => x.Principal.Id.ToString()));
  }

  [Fact]
  public async Task Keyset_paging_resumes_a_same_timestamp_group_in_postgres_uuid_order_across_the_page_boundary()
  {
    // A 105-row result whose final 10 rows share one CreatedAt, straddling the
    // 100-row page boundary: 95 earlier-timestamped fillers open page 1, then
    // the first 5 of the tied group finish it. The cursor handed to page 2
    // therefore carries that shared timestamp AND a UUID tiebreak, so page 2
    // must resume mid-group without dropping or repeating a row. The tie is
    // broken by PostgreSQL's unsigned big-endian byte order, not the default
    // mixed-endian Guid.ToByteArray(); these 10 ids are chosen so those two
    // orders genuinely disagree, so a fake comparing the wrong bytes would sort
    // the group differently and fail this test.
    var sharedTimestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(1000);
    var tiedIdsInPostgresOrder = new[]
    {
      "00000000-0000-0000-0000-000000000001",
      "00000000-0000-0000-0000-010000000000",
      "00000000-0000-0000-0100-000000000000",
      "00000000-0000-0001-0000-000000000000",
      "00000000-0001-0000-0000-000000000000",
      "00000000-0100-0000-0000-000000000000",
      "00000001-0000-0000-0000-000000000000",
      "00000100-0000-0000-0000-000000000000",
      "00010000-0000-0000-0000-000000000000",
      "01000000-0000-0000-0000-000000000000",
    }.Select(Guid.Parse).ToArray();

    // Fillers use indices 100..194 so their ids never collide with the tied
    // group and their default createdAt (base + index minutes) stays strictly
    // before the shared timestamp.
    var fillers = Enumerable.Range(100, 95).Select(index => MakeWithdrawal(index)).ToArray();
    // Insert the tied group scrambled so the fixture proves the fake sorts them
    // rather than echoing input order.
    var scrambledTied = new[] { 4, 9, 0, 7, 2, 5, 1, 8, 3, 6 }
      .Select(i => WithId(MakeWithdrawal(0), tiedIdsInPostgresOrder[i], sharedTimestamp))
      .ToArray();
    var repository = new FakeExportRepository(fillers.Concat(scrambledTied));
    var exporter = new WithdrawalExporter(repository, new FakeStorage());

    var prepared = await exporter.Prepare(new WithdrawalSearch());
    using var writer = new StringWriter();
    var written = await exporter.Write(prepared.SuccessOrDefault(), writer);

    written.IsSuccess().Should().BeTrue();
    var ids = writer
      .ToString()
      .Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries)
      .Skip(1)
      .Select(record => record.Split(',')[0])
      .ToArray();
    var expected = fillers
      .Select(w => w.Principal.Id)
      .Concat(tiedIdsInPostgresOrder)
      .Select(id => id.ToString())
      .ToArray();

    ids.Should().HaveCount(105).And.OnlyHaveUniqueItems();
    ids.Should().Equal(
      expected,
      "the tied group resumes across the page boundary in PostgreSQL big-endian UUID order"
    );
    // The boundary really fell inside the tied group: page 2's cursor carries
    // the shared timestamp and the 5th tied id, proving the same-CreatedAt
    // keyset branch ran (not a timestamp-only cursor).
    repository.IssuedSearches.Should().HaveCount(2);
    repository.IssuedSearches[1].Cursor!.CreatedAt.Should().Be(sharedTimestamp);
    repository.IssuedSearches[1].Cursor!.Id.Should().Be(tiedIdsInPostgresOrder[4]);
  }

  [Fact]
  public async Task Exactly_full_pages_request_a_final_empty_cursor_page()
  {
    var withdrawals = Enumerable.Range(0, 200).Select(index => MakeWithdrawal(index)).ToArray();
    var repository = new FakeExportRepository(withdrawals);
    var exporter = new WithdrawalExporter(repository, new FakeStorage());

    var prepared = await exporter.Prepare(new WithdrawalSearch());
    using var writer = new StringWriter();
    var written = await exporter.Write(prepared.SuccessOrDefault(), writer);

    written.IsSuccess().Should().BeTrue();
    repository.IssuedSearches.Should().HaveCount(3);
    repository.IssuedSearches[0].Cursor.Should().BeNull();
    repository.IssuedSearches[1].Cursor.Should().Be(CursorFor(withdrawals[99]));
    repository.IssuedSearches[2].Cursor.Should().Be(CursorFor(withdrawals[199]));
    writer
      .ToString()
      .Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries)
      .Should()
      .HaveCount(201);
  }

  [Fact]
  public async Task Storage_failure_during_prepare_propagates_before_any_output_is_started()
  {
    var storage = new FakeStorage { FailGet = true };
    var exporter = new WithdrawalExporter(
      new FakeExportRepository([MakeWithdrawal(1, receipt: "receipts/fails.pdf")]),
      storage
    );

    var prepared = await exporter.Prepare(new WithdrawalSearch());

    prepared.IsFailure().Should().BeTrue();
  }

  [Fact]
  public async Task Export_accepts_a_mixed_case_owner_role_and_streams_the_csv()
  {
    await using var serviceProvider = AuthenticationServices(authenticated: true);
    await using var responseBody = new MemoryStream();
    var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
    httpContext.Response.Body = responseBody;
    var exporter = new RecordingExporter(
      new WithdrawalExporter(new FakeExportRepository([MakeWithdrawal(1)]), new FakeStorage())
    );
    var controller = Controller(
      httpContext,
      exporter,
      new ExportWithdrawalQueryValidator(),
      ["OwNeR"]
    );
    ActionResult? result = null;
    var nextCalls = 0;
    var authPipeline = ExportAuthenticationPipeline(
      serviceProvider,
      async _ =>
      {
        nextCalls++;
        result = await controller.Export(
          new ExportWithdrawalQuery(null, null, null, null, null, null, null, null)
        );
      }
    );
    SetExportEndpoint(httpContext);

    await authPipeline(httpContext);

    nextCalls.Should().Be(1, "plain [Authorize] must admit an authenticated owner without admin");
    httpContext.User.FindAll(AuthRoles.Field).Select(claim => claim.Value).Should().Equal("OwNeR");
    result.Should().BeOfType<EmptyResult>();
    exporter.PrepareCalls.Should().Be(1);
    exporter.WriteCalls.Should().Be(1);
    controller.Response.ContentType.Should().Be("text/csv; charset=utf-8");
    Encoding.UTF8.GetString(responseBody.ToArray())
      .Split(WithdrawalCsv.LineEnding, StringSplitOptions.RemoveEmptyEntries)
      .Should()
      .HaveCount(2, "the header plus the single matching withdrawal");
  }

  [Fact]
  public async Task Admin_without_owner_is_refused_before_validation_or_export_with_readable_403()
  {
    await using var serviceProvider = new ServiceCollection()
      .AddLogging()
      .AddProblemDetailsService(new ErrorPortalOption { Enabled = false }, new AppOption())
      .AddControllers()
      .Services.BuildServiceProvider();
    await using var responseBody = new MemoryStream();
    var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
    httpContext.Request.Headers.Accept = "text/csv";
    httpContext.Response.Body = responseBody;
    var exporter = new RecordingExporter(
      new WithdrawalExporter(new FakeExportRepository([]), new FakeStorage())
    );
    var controller = Controller(
      httpContext,
      exporter,
      new ExportWithdrawalQueryValidator(),
      [AuthRoles.Admin]
    );

    // The query is deliberately invalid: a 403 (not a 400) proves the owner
    // guard ran before validation, and the exporter was never reached.
    var result = await controller.Export(
      new ExportWithdrawalQuery(null, null, null, null, null, "Cancelled", null, null)
    );

    var statusResult = result.Should().BeOfType<StatusCodeResult>().Which;
    statusResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    exporter.PrepareCalls.Should().Be(0);
    exporter.WriteCalls.Should().Be(0);

    // [ApiController] uses this factory for IClientErrorActionResult values;
    // executing its result exercises the real problem-details formatter and
    // CSV-only Accept negotiation instead of manufacturing a response body.
    var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    var clientError = serviceProvider
      .GetRequiredService<IClientErrorFactory>()
      .GetClientError(actionContext, statusResult);
    clientError.Should().NotBeNull();
    await clientError!.ExecuteResultAsync(actionContext);

    httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    var body = Encoding.UTF8.GetString(responseBody.ToArray());
    body.Should().NotBeEmpty("the CSV Accept header must not suppress the problem document");
    body.Should()
      .Contain("Unauthorized")
      .And.Contain("You are not authorized to access this resource")
      .And.Contain(AuthRoles.Owner);
  }

  [Fact]
  public async Task Unauthenticated_callers_are_challenged_with_401_by_the_export_endpoint_metadata()
  {
    await using var serviceProvider = AuthenticationServices(authenticated: false);
    var nextCalls = 0;
    var authPipeline = ExportAuthenticationPipeline(
      serviceProvider,
      _ =>
      {
        nextCalls++;
        return Task.CompletedTask;
      }
    );
    var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
    SetExportEndpoint(httpContext);

    await authPipeline(httpContext);

    httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    nextCalls.Should().Be(0, "an unauthenticated caller must never reach the export action");
  }

  [Fact]
  public async Task Export_response_exposes_the_attachment_filename_to_cross_origin_clients()
  {
    await using var responseBody = new MemoryStream();
    var httpContext = new DefaultHttpContext();
    httpContext.Response.Body = responseBody;
    var controller = Controller(
      httpContext,
      new WithdrawalExporter(new FakeExportRepository([]), new FakeStorage()),
      new ExportWithdrawalQueryValidator()
    );

    var result = await controller.Export(
      new ExportWithdrawalQuery(null, null, null, null, null, null, null, null)
    );

    result.Should().BeOfType<EmptyResult>();
    controller.Response.ContentType.Should().Be("text/csv; charset=utf-8");
    controller
      .Response.Headers.ContentDisposition.ToString()
      .Should()
      .Be("attachment; filename=\"withdrawals-earliest_latest.csv\"");
    controller
      .Response.Headers["Access-Control-Expose-Headers"]
      .ToString()
      .Should()
      .Be("Content-Disposition");
    controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    responseBody.ToArray().Take(3).Should().Equal(new byte[] { 0xef, 0xbb, 0xbf });
  }

  [Theory]
  [InlineData("Cancelled", null, "Status must be one of")]
  [InlineData(null, "2026-12-31", "DateOnly must be in the format of dd-MM-yyyy")]
  public async Task Invalid_export_query_can_be_written_as_readable_400_problem_details(
    string? status,
    string? before,
    string expectedMessage
  )
  {
    await using var serviceProvider = new ServiceCollection()
      .AddLogging()
      .AddProblemDetailsService(
        new ErrorPortalOption { Enabled = false },
        new AppOption()
      )
      .BuildServiceProvider();
    await using var responseBody = new MemoryStream();
    var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
    httpContext.Request.Headers.Accept =
      "text/csv, application/problem+json, application/json";
    httpContext.Response.Body = responseBody;
    var controller = Controller(httpContext, null!, new ExportWithdrawalQueryValidator());

    var result = await controller.Export(
      new ExportWithdrawalQuery(null, null, null, null, null, status, before, null)
    );

    var statusResult = result.Should().BeOfType<StatusCodeResult>().Which;
    statusResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    httpContext.Response.StatusCode = statusResult.StatusCode;
    var written = await serviceProvider
      .GetRequiredService<IProblemDetailsService>()
      .TryWriteAsync(
        new ProblemDetailsContext
        {
          HttpContext = httpContext,
          ProblemDetails = new ProblemDetails { Status = StatusCodes.Status400BadRequest },
        }
      );

    written.Should().BeTrue();
    httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    httpContext.Response.ContentType.Should().StartWith("application/problem+json");
    Encoding.UTF8.GetString(responseBody.ToArray())
      .Should()
      .Contain("Validation Error")
      .And.Contain(expectedMessage);
  }

  private static ServiceProvider AuthenticationServices(bool authenticated) =>
    new ServiceCollection()
      .AddLogging()
      .AddAuthorization()
      .AddAuthentication(TestAuthenticationScheme.Name)
      .AddScheme<TestAuthenticationOptions, TestAuthenticationScheme>(
        TestAuthenticationScheme.Name,
        options => options.Authenticated = authenticated
      )
      .Services.BuildServiceProvider();

  private static RequestDelegate ExportAuthenticationPipeline(
    IServiceProvider serviceProvider,
    RequestDelegate next
  )
  {
    var authorization = new AuthorizationMiddleware(
      next,
      serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>()
    );
    var authentication = new AuthenticationMiddleware(
      authorization.Invoke,
      serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>()
    );
    return authentication.Invoke;
  }

  private static void SetExportEndpoint(HttpContext httpContext) =>
    httpContext.SetEndpoint(
      new Endpoint(
        _ => Task.CompletedTask,
        new EndpointMetadataCollection(
          typeof(WithdrawalController)
            .GetMethod(nameof(WithdrawalController.Export))!
            .GetCustomAttributes(inherit: true)
        ),
        nameof(WithdrawalController.Export)
      )
    );

  // Export is owner-only, so every export test needs a caller: the default
  // grants the owner role, and the authorization tests override it.
  private static WithdrawalController Controller(
    DefaultHttpContext httpContext,
    IWithdrawalExporter exporter,
    ExportWithdrawalQueryValidator exportValidator,
    string[]? roles = null
  ) =>
    new(
      null!,
      null!,
      null!,
      null!,
      null!,
      exportValidator,
      null!,
      null!,
      exporter,
      null!,
      null!,
      new FakeAuthHelper(roles ?? [AuthRoles.Owner])
    )
    {
      ControllerContext = new ControllerContext { HttpContext = httpContext },
    };

  private static Withdrawal MakeWithdrawal(
    int index,
    string? receipt = null,
    DateTime? createdAt = null,
    DateTime? completedAt = null
  )
  {
    var id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}");
    return new Withdrawal
    {
      Principal = new WithdrawalPrincipal
      {
        Id = id,
        CreatedAt = createdAt
          ?? new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index),
        Status = new WithdrawalStatus { Status = WithdrawStatus.Completed },
        Record = new WithdrawalRecord
        {
          Amount = 100m,
          Method = WithdrawalMethod.PayNow,
          PayNowNumber = "91234567",
        },
        Complete = receipt is null
          ? null
          : new WithdrawalComplete
          {
            CompletedAt = completedAt
              ?? new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            CompleterId = "admin-user",
            Note = "completed",
            Receipt = receipt,
          },
        Payout = new WithdrawalPayout
        {
          ConfirmationNumber = "confirmation",
          Fee = 1m,
          Attempt = 1,
          ReconcileAttempts = 0,
        },
      },
      Wallet = new WalletPrincipal
      {
        Id = Guid.NewGuid(),
        UserId = "accountant-user",
        Record = new WalletRecord { Usable = 0m, WithdrawReserve = 0m, BookingReserve = 0m },
      },
      User = new UserPrincipal
      {
        Id = "accountant-user",
        Record = new UserRecord { Username = "accountant", Email = "accountant@example.test" },
      },
      Completer = null,
    };
  }

  private static Withdrawal WithId(Withdrawal withdrawal, Guid id, DateTime createdAt) =>
    withdrawal with
    {
      Principal = withdrawal.Principal with { Id = id, CreatedAt = createdAt },
    };

  private static WithdrawalExportCursor CursorFor(Withdrawal withdrawal) =>
    new(withdrawal.Principal.CreatedAt, withdrawal.Principal.Id);

  private sealed class FakeExportRepository(IEnumerable<Withdrawal> withdrawals)
    : IWithdrawalExportRepository
  {
    private readonly List<Withdrawal> withdrawals = withdrawals.ToList();

    public List<(WithdrawalSearch Search, int Limit, WithdrawalExportCursor? Cursor)> IssuedSearches { get; } =
      [];

    public WithdrawalExportCursor? FailAtCursor { get; init; }

    public Action<FakeExportRepository>? AfterFirstPage { get; set; }

    public void Remove(Guid id) => this.withdrawals.RemoveAll(x => x.Principal.Id == id);

    // PostgreSQL orders `uuid` by an unsigned memcmp of the 16 canonical
    // (RFC 4122, big-endian) bytes, and Npgsql translates both the keyset
    // cursor predicate and `ORDER BY "Id"` to that order. The fake reproduces
    // it exactly so the tiebreak test reflects production. Empirically this
    // coincides with Guid.CompareTo across 2M random pairs (verified) — the
    // trap is the DEFAULT Guid.ToByteArray(), which is mixed-endian and yields
    // a genuinely different order; using the explicit big-endian bytes keeps
    // the fake pinned to Postgres semantics regardless of Guid internals.
    private static readonly IComparer<Guid> PostgresUuidOrder = Comparer<Guid>.Create(
      PostgresUuidCompare
    );

    private static int PostgresUuidCompare(Guid a, Guid b) =>
      a.ToByteArray(bigEndian: true).AsSpan().SequenceCompareTo(b.ToByteArray(bigEndian: true));

    public Task<Result<IReadOnlyList<Withdrawal>>> SearchExport(
      WithdrawalSearch search,
      int limit,
      WithdrawalExportCursor? cursor = null,
      CancellationToken cancellationToken = default
    )
    {
      this.IssuedSearches.Add((search, limit, cursor));
      cancellationToken.ThrowIfCancellationRequested();
      if (limit > WithdrawalExporter.PageSize)
      {
        return Task.FromResult(
          (Result<IReadOnlyList<Withdrawal>>)new InvalidOperationException("page exceeds 100 rows")
        );
      }

      if (this.FailAtCursor is not null && cursor == this.FailAtCursor)
      {
        return Task.FromResult(
          (Result<IReadOnlyList<Withdrawal>>)new InvalidOperationException("repository unavailable")
        );
      }

      var page = this.withdrawals
        .Where(x =>
          cursor is null
          || x.Principal.CreatedAt > cursor.CreatedAt
          || (
            x.Principal.CreatedAt == cursor.CreatedAt
            && PostgresUuidCompare(x.Principal.Id, cursor.Id) > 0
          )
        )
        .OrderBy(x => x.Principal.CreatedAt)
        .ThenBy(x => x.Principal.Id, PostgresUuidOrder)
        .Take(limit)
        .ToArray();
      if (this.IssuedSearches.Count == 1)
        this.AfterFirstPage?.Invoke(this);
      return Task.FromResult((Result<IReadOnlyList<Withdrawal>>)page);
    }
  }

  private sealed class FakeStorage : IWithdrawalStorage
  {
    public List<string> RequestedKeys { get; } = [];

    public List<TimeSpan> RequestedExpiries { get; } = [];

    public bool FailGet { get; init; }

    public Task<Result<string>> Save(Stream stream) => Task.FromResult((Result<string>)"unused");

    public Task<Result<string>> Get(string key) => this.Get(key, TimeSpan.FromHours(1));

    public Task<Result<string>> Get(string key, TimeSpan expiry)
    {
      this.RequestedKeys.Add(key);
      this.RequestedExpiries.Add(expiry);
      return this.FailGet
        ? Task.FromResult((Result<string>)new InvalidOperationException("storage unavailable"))
        : Task.FromResult((Result<string>)$"https://storage.test/{key}");
    }
  }

  private sealed class RecordingExporter(IWithdrawalExporter inner) : IWithdrawalExporter
  {
    public int PrepareCalls { get; private set; }

    public int WriteCalls { get; private set; }

    public Task<Result<PreparedWithdrawalExport>> Prepare(
      WithdrawalSearch search,
      CancellationToken cancellationToken = default
    )
    {
      this.PrepareCalls++;
      return inner.Prepare(search, cancellationToken);
    }

    public Task<Result<Unit>> Write(
      PreparedWithdrawalExport export,
      TextWriter writer,
      CancellationToken cancellationToken = default
    )
    {
      this.WriteCalls++;
      return inner.Write(export, writer, cancellationToken);
    }
  }

  private sealed class FakeAuthHelper(string[] roles) : IAuthHelper
  {
    public bool HasAll(
      System.Security.Claims.ClaimsPrincipal? user,
      string field,
      params string[] scopes
    ) => scopes.All(scope => roles.Contains(scope));

    public bool HasAny(
      System.Security.Claims.ClaimsPrincipal? user,
      string field,
      params string[] scopes
    ) => scopes.Any(scope => roles.Contains(scope));

    public IEnumerable<string> FieldToScope(
      System.Security.Claims.ClaimsPrincipal? user,
      string field
    ) => field == AuthRoles.Field ? roles : [];

    public ILogger<IAuthHelper> Logger => NullLogger<IAuthHelper>.Instance;
  }

  private sealed class TestAuthenticationOptions : AuthenticationSchemeOptions
  {
    public bool Authenticated { get; set; }
  }

  private sealed class TestAuthenticationScheme(
    IOptionsMonitor<TestAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
  ) : AuthenticationHandler<TestAuthenticationOptions>(options, logger, encoder)
  {
    public const string Name = "ExportAuthenticationTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      if (!this.Options.Authenticated)
        return Task.FromResult(AuthenticateResult.NoResult());

      var principal = new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(AuthRoles.Field, "OwNeR")], this.Scheme.Name)
      );
      return Task.FromResult(
        AuthenticateResult.Success(new AuthenticationTicket(principal, this.Scheme.Name))
      );
    }
  }

  private sealed class CancellingStringWriter(
    CancellationTokenSource cancellation,
    int lineEndingLimit
  ) : StringWriter
  {
    private int lineEndings;

    public override async Task WriteAsync(string? value)
    {
      await base.WriteAsync(value);
      if (value == WithdrawalCsv.LineEnding && ++this.lineEndings == lineEndingLimit)
        await cancellation.CancelAsync();
    }
  }

}
