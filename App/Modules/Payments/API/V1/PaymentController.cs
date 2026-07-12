using System.Net.Mime;
using System.Text.Encodings.Web;
using System.Text.Json;
using App.Error.V1;
using App.Modules.Common;
using App.Modules.Payments.Airwallex;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain;
using Domain.Payment;
using Domain.Wallet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Payments.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class PaymentController(
  IPaymentService service,
  IWalletService walletService,
  SearchPaymentQueryValidator searchPaymentQueryValidator,
  CreatePaymentReqValidator createPaymentReqValidator,
  AirwallexWebhookService webhookService,
  IAuthHelper authHelper
) : AtomiControllerBase(authHelper)
{
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet]
  public async Task<ActionResult<IEnumerable<PaymentPrincipalRes>>> Search(
    [FromQuery] SearchPaymentQuery query
  )
  {
    var x = await searchPaymentQueryValidator
      .ValidateAsyncResult(query, "Invalid SearchPaymentQuery")
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // intent-level evidence rows for the admin Analysis page: which payment
  // intents captured money in the (inclusive SGT date) range, newest first,
  // capped at 100 rows
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("captured")]
  public async Task<ActionResult<IEnumerable<CapturedPaymentRes>>> Captured(
    [FromQuery] CapturedPaymentsQueryReq query,
    [FromServices] CapturedPaymentsQueryReqValidator capturedValidator
  )
  {
    var x = await capturedValidator
      .ValidateAsyncResult(query, "Invalid CapturedPaymentsQueryReq")
      .ThenAwait(q => service.ListCaptured(q.ToDomain()))
      .Then(r => r.Select(p => p.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // Pull Airwallex's own fees (financial transactions) for money movements
  // in the (inclusive SGT date) range that have no fee rows yet: captured
  // intents, completed PayNow transfers and settled card-refund fragments.
  // Idempotent (upsert by financial transaction id); sources the gateway has
  // not posted fees for yet come back in missing — sync again later. Bounded
  // per call (hasMore = call again to continue).
  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("gateway-fees/sync")]
  public async Task<ActionResult<GatewayFeeSyncRes>> SyncGatewayFees(
    [FromQuery] GatewayFeeSyncQueryReq query,
    [FromServices] GatewayFeeSyncQueryReqValidator syncValidator,
    [FromServices] IGatewayFeeSyncService syncService
  )
  {
    var x = await syncValidator
      .ValidateAsyncResult(query, "Invalid GatewayFeeSyncQueryReq")
      .ThenAwait(q => syncService.Sync(q.ToDomain()))
      .Then(r => r.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("id/{id:guid}")]
  public async Task<ActionResult<PaymentRes>> GetById(Guid id)
  {
    var x = await service.GetById(id).Then(x => x?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Payment Not Found", typeof(PaymentPrincipal), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("reference/{reference}")]
  public async Task<ActionResult<PaymentRes>> GetByRef(string reference)
  {
    var x = await service.GetByRef(reference).Then(x => x?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Payment Not Found", typeof(PaymentPrincipal), reference)
    );
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("id/{id:guid}")]
  public async Task<ActionResult<PaymentRes>> DelById(Guid id)
  {
    var x = await service.DeleteById(id);
    return this.ReturnUnitNullableResult(
      x,
      new EntityNotFound("Payment Not Found", typeof(PaymentPrincipal), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("reference/{reference}")]
  public async Task<ActionResult<PaymentRes>> DelByRef(string reference)
  {
    var x = await service.DeleteByRef(reference);
    return this.ReturnUnitNullableResult(
      x,
      new EntityNotFound("Payment Not Found", typeof(PaymentPrincipal), reference)
    );
  }

  [Authorize, HttpPost("{walletId:guid}")]
  public async Task<ActionResult<CreatePaymentRes>> CreatePayment(
    Guid walletId,
    string userId,
    [FromBody] CreatePaymentReq req
  )
  {
    var p = await createPaymentReqValidator
      .ValidateAsyncResult(req, "Invalid CreatePaymentReq")
      .ThenAwait(r =>
        walletService
          .Get(walletId, userId)
          .NullToError(walletId.ToString())
          .ThenAwait(x => service.Create(x.Principal.Id, r.Amount, r.Currency, Guid.NewGuid()))
          .Then(x => x.Item1.ToRes(x.Item2), Errors.MapNone)
      );

    return this.ReturnResult(p);
  }

  [HttpPost("webhook")]
  public async Task<ActionResult> Webhook(AirwallexEvent evt)
  {
    this.Request.Headers.TryGetValue("x-timestamp", out var timestamp);
    this.Request.Headers.TryGetValue("x-signature", out var signature);

    this.Request.Body.Seek(0, SeekOrigin.Begin);
    using var stream = new StreamReader(this.HttpContext.Request.Body);
    var body = await stream.ReadToEndAsync();
    var o = body.ToObj<object>();
    var payload = JsonSerializer.Serialize(
      o,
      new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
    );
    var r = await webhookService.ProcessEvent(
      evt,
      timestamp.ToString(),
      payload,
      signature.ToString()
    );

    return this.ReturnUnitResult(r);
  }
}
