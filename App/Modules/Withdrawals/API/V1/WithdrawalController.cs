using System.Net.Mime;
using System.Text;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Options;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain;
using Domain.Wallet;
using Domain.Withdrawal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace App.Modules.Withdrawals.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class WithdrawalController(
  IWithdrawalService service,
  CreateWithdrawalReqValidator createWithdrawalReqValidator,
  RejectWithdrawalReqValidator rejectWithdrawalReqValidator,
  CancelWithdrawalReqValidator cancelWithdrawalReqValidator,
  SearchWithdrawalQueryValidator searchWithdrawalQueryValidator,
  ExportWithdrawalQueryValidator exportWithdrawalQueryValidator,
  SetWithdrawalSettingsReqValidator setWithdrawalSettingsReqValidator,
  IWithdrawalImageEnricher enrich,
  IWithdrawalExporter exporter,
  IFeeCalculator feeCalculator,
  IOptionsSnapshot<WithdrawalOption> withdrawalOptions,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  private int RefundWindowDays => withdrawalOptions.Value.RefundWindowDays;

  // DEPRECATED: kept only so already-deployed frontends keep working during
  // rollout — use GET Fee/{type} instead, which also carries the flat
  // component. Returns the percentage as a rate, e.g. 0.04 = 4%.
  [Authorize, HttpGet("fee")]
  public async Task<ActionResult<FeeRes>> Fee()
  {
    var x = await feeCalculator
      .Current(FeeType.Withdrawal)
      .Then(s => new FeeRes(s.Percentage / 100m), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize, HttpGet]
  public async Task<ActionResult<IEnumerable<WithdrawalPrincipalRes>>> Search(
    [FromQuery] SearchWithdrawalQuery query
  )
  {
    var x = await this.GuardOrAllAsync(query.UserId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ =>
        searchWithdrawalQueryValidator.ValidateAsyncResult(query, "Invalid SearchWithdrawalQuery")
      )
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapNone)
      .ThenAwait(x => enrich.Enrich(x));

    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("export")]
  [ProducesResponseType<string>(StatusCodes.Status200OK, "text/csv")]
  public async Task<ActionResult> Export([FromQuery] ExportWithdrawalQuery query)
  {
    var validated = await exportWithdrawalQueryValidator.ValidateAsyncResult(
      query,
      "Invalid ExportWithdrawalQuery"
    );
    if (validated.IsFailure())
      return this.ReturnUnitResult((Result<Unit>)validated.FailureOrDefault());

    PreparedWithdrawalExport prepared;
    try
    {
      var preparedResult = await exporter.Prepare(
        validated.SuccessOrDefault().ToDomain(),
        this.HttpContext.RequestAborted
      );
      if (preparedResult.IsFailure())
        return this.ReturnUnitResult((Result<Unit>)preparedResult.FailureOrDefault());
      prepared = preparedResult.SuccessOrDefault();
    }
    catch (OperationCanceledException) when (this.HttpContext.RequestAborted.IsCancellationRequested)
    {
      this.HttpContext.Abort();
      return new EmptyResult();
    }

    this.Response.ContentType = "text/csv; charset=utf-8";
    this.Response.Headers.ContentDisposition =
      $"attachment; filename=\"{WithdrawalCsv.FileName(query.After, query.Before)}\"";
    this.Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
    this.Response.Headers.CacheControl = "no-store";

    try
    {
      Result<Unit> written;
      {
        await using var writer = new StreamWriter(
          this.Response.Body,
          new UTF8Encoding(false),
          bufferSize: 64 * 1024,
          leaveOpen: true
        );
        written = await exporter.Write(prepared, writer, this.HttpContext.RequestAborted);
        await writer.FlushAsync();
      }
      if (written.IsFailure())
        this.HttpContext.Abort();
    }
    catch (OperationCanceledException) when (this.HttpContext.RequestAborted.IsCancellationRequested)
    {
      this.HttpContext.Abort();
    }
    catch
    {
      this.HttpContext.Abort();
      throw;
    }

    return new EmptyResult();
  }

  [Authorize, HttpGet("{id:guid}")]
  public async Task<ActionResult<WithdrawalRes>> Get(Guid id, string? userId)
  {
    var wallet = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.Get(id, userId))
      .Then(x => x?.ToRes(), Errors.MapNone)
      .ThenAwait(x => Utility.Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));

    return this.ReturnNullableResult(
      wallet,
      new EntityNotFound("Wallet Not Found", typeof(Wallet), id.ToString())
    );
  }

  [Authorize, HttpPost("{userId}")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Create(
    string userId,
    [FromBody] CreateWithdrawalReq req
  )
  {
    var withdrawal = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ =>
        createWithdrawalReqValidator.ValidateAsyncResult(req, "Invalid CreateWithdrawalReq")
      )
      .ThenAwait(r => service.Create(userId, r.ToDomain(), this.RefundWindowDays))
      .Then(x => x.ToRes(), Errors.MapNone)
      .ThenAwait(x => enrich.Enrich(x));

    return this.ReturnResult(withdrawal);
  }

  [Authorize, HttpPost("{userId}/{id:guid}/cancel")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Cancel(
    Guid id,
    string userId,
    [FromBody] CancelWithdrawalReq req
  )
  {
    var withdrawal = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ =>
        cancelWithdrawalReqValidator
          .ValidateAsyncResult(req, "Invalid CancelWithdrawalReq")
          .ThenAwait(r => service.Cancel(id, userId, r.Note))
          .Then(x => x.ToRes(), Errors.MapNone)
      )
      .ThenAwait(enrich.Enrich);

    return this.ReturnResult(withdrawal);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("{id:guid}/reject")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Reject(
    Guid id,
    [FromBody] RejectWithdrawalReq req
  )
  {
    var userId = this.Sub();
    var withdrawal = await rejectWithdrawalReqValidator
      .ValidateAsyncResult(req, "Invalid RejectWithdrawalReq")
      .ThenAwait(x => service.Reject(id, userId!, req.Note))
      .Then(x => x.ToRes(), Errors.MapNone)
      .ThenAwait(enrich.Enrich);

    return this.ReturnResult(withdrawal);
  }

  // Initiates the automated Airwallex payout: Pending -> Processing; the
  // gateway webhooks complete (or fail/park) the withdrawal. PayNow creates
  // a transfer; CardRefund creates refunds against the payments that funded
  // the wallet. Callable by admins (argon Approve button) and by tin's
  // withdrawer sweep — no caller is assumed, the flow is fully interactive-
  // safe (the tin nightly sweep is being disabled by config; approve then
  // happens on an admin click and behaves identically).
  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("{id:guid}/approve")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Approve(Guid id)
  {
    var x = await service
      .Approve(id, this.RefundWindowDays)
      .Then(w => w.ToRes(), Errors.MapNone)
      .ThenAwait(enrich.Enrich);
    return this.ReturnResult(x);
  }

  // The withdrawal settings in effect right now (defaults when never
  // configured). Readable by ANY authenticated caller: the create dialog
  // needs the method availability for regular users, and tin's sweep polls
  // SweepEnabled — plain [Authorize] admits tin's M2M token, since it is the
  // same JWT bearer authentication the AdminOrTin endpoints already pass
  // (the policy only ADDS a roles check on top).
  [Authorize, HttpGet("settings/current")]
  public async Task<ActionResult<WithdrawalSettingsRes>> GetSettings()
  {
    var x = await service.GetCurrentSettings().Then(s => s.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // Replace the withdrawal settings (insert-only latest, like PrioritySettings)
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("settings")]
  public async Task<ActionResult<WithdrawalSettingsRes>> SetSettings(
    [FromBody] SetWithdrawalSettingsReq req
  )
  {
    var x = await setWithdrawalSettingsReqValidator
      .ValidateAsyncResult(req, "Invalid SetWithdrawalSettingsReq")
      .ThenAwait(r => service.CreateSettings(r.ToDomain()))
      .Then(s => s.Record.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // How much the user could withdraw via card refunds right now: captured
  // card payments inside the refund window, minus refunds already issued
  // against them. Owner-or-staff visibility, same as the withdrawal listing.
  [Authorize, HttpGet("refundable/{userId}")]
  public async Task<ActionResult<RefundablePoolRes>> Refundable(string userId)
  {
    var x = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.RefundablePool(userId, this.RefundWindowDays))
      .Then(pool => new RefundablePoolRes(pool, this.RefundWindowDays), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // Admin escape hatch: finalize a confirmed Processing withdrawal whose
  // settled webhook was permanently lost, after checking the transfer on the
  // Airwallex dashboard. Idempotent — takes the same path the webhook would.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("{id:guid}/complete-payout")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> ForceCompletePayout(Guid id)
  {
    var userId = this.Sub();
    var x = await service
      .ForceCompletePayout(id, userId!)
      .Then(w => w.ToRes(), Errors.MapNone)
      .ThenAwait(enrich.Enrich);
    return this.ReturnResult(x);
  }

  // Reconciliation sweep entry (tin every 6h, or an admin): looks the
  // transfer up at the gateway and settles/fails/parks accordingly
  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("{id:guid}/reconcile")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Reconcile(Guid id)
  {
    var x = await service
      .Reconcile(id)
      .Then(w => w.ToRes(), Errors.MapNone)
      .ThenAwait(enrich.Enrich);
    return this.ReturnResult(x);
  }

  // Admin resolution for a parked withdrawal: back to Pending for another
  // automated attempt (only after verifying no live transfer at the gateway)
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("{id:guid}/requeue")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Requeue(Guid id)
  {
    var x = await service
      .Requeue(id)
      .Then(w => w.ToRes(), Errors.MapNone)
      .ThenAwait(enrich.Enrich);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin)]
  [HttpPost("{id:guid}/complete")]
  [Consumes(MediaTypeNames.Multipart.FormData)]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Complete(Guid id, IFormFile file)
  {
    var userId = this.Sub();
    using var stream = new MemoryStream();

    await file.CopyToAsync(stream);
    var x = await service
      .Complete(id, userId!, "", stream)
      .Then(x => x.ToRes(), Errors.MapNone)
      .ThenAwait(enrich.Enrich);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("{id:guid}")]
  public async Task<ActionResult<WithdrawalPrincipalRes>> Delete(Guid id)
  {
    var user = await service.Delete(id);
    return this.ReturnUnitNullableResult(
      user,
      new EntityNotFound("User Not Found", typeof(WithdrawalPrincipal), id.ToString())
    );
  }
}
