using System.Net.Mime;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.Invoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Invoices.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class InvoiceController(
  IInvoiceInputRepository inputRepo,
  IInvoiceSettingsRepository settingsRepo,
  InvoiceInputQueryReqValidator inputQueryValidator,
  SetInvoiceSettingsReqValidator settingsValidator,
  SetInvoicePartnerReqValidator partnerValidator,
  IAuthHelper helper
) : AtomiControllerBase(helper)
{
  // Every figure the partner invoice needs for one SGT month, gathered from
  // zinc's own records. This replaces the hand-assembly step of the invoice
  // toolchain — it does NOT compute the invoice: no profit, no partner split,
  // no rounding decisions. The engine owns those.
  //
  // Reports per direction (route) for tickets, revenue, terminations and
  // priority fees; month-level for deposits, gateway fees and withdrawals.
  //
  // SNAPSHOT: booking status is mutated in place, so re-gathering a past month
  // will not reproduce the invoice issued at the time (June: 2,713 tickets
  // issued, 2,672 Completed today). Callers issuing a document must freeze
  // this payload rather than re-deriving it later.
  //
  // TWO gates on purpose, matching Withdrawal/export: the OnlyAdmin policy
  // keeps customer JWTs and tin's M2M token out of the action body entirely,
  // and the owner check narrows that to owners. Without the policy the
  // in-method guard would be the single thing between any authenticated
  // caller and full financial figures, so a refactor that dropped or
  // reordered one line would fail open.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("inputs")]
  public async Task<ActionResult<InvoiceInputRowRes>> Inputs(
    [FromQuery] InvoiceInputQueryReq query
  )
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => inputQueryValidator.ValidateAsyncResult(query, "Invalid InvoiceInputQueryReq"))
      .ThenAwait(q => inputRepo.Gather(q.ToDomain()))
      .Then(r => r.ToRes(), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // The agreed terms in force right now, plus anything queued ahead of it.
  // `current` is null when the terms are not usable — see InvoiceSettingsRes.
  //
  // Same two gates as above: these are the partnership's commercial terms,
  // not operational config.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("settings")]
  public async Task<ActionResult<InvoiceSettingsRes>> Settings()
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => settingsRepo.ListSettings())
      .ThenAwait(settings =>
        settingsRepo
          .ListPartners()
          .Then(
            partners => InvoiceSettingsSchedule.View(settings, partners, DateTime.UtcNow).ToRes(),
            Errors.MapNone
          )
      )
      .Then(x => x, Errors.MapAll);
    return this.ReturnResult(x);
  }

  // Queue a change to the agreed terms (immediate when EffectiveAt is
  // omitted). Insert-only: this never edits the live row, because an invoice
  // already issued under the old terms has to stay explicable.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("settings")]
  public async Task<ActionResult<InvoiceSettingsChangeRes>> SetSettings(
    [FromBody] SetInvoiceSettingsReq req
  )
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => settingsValidator.ValidateAsyncResult(req, "Invalid SetInvoiceSettingsReq"))
      .ThenAwait(r => settingsRepo.AddSettings(r.ToDomain(), r.EffectiveAt))
      .Then(c => c.ToRes(), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // Queue a change to one partner's terms, keyed by Suffix. Retiring a
  // partner is Active = false, never a delete.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("settings/partners")]
  public async Task<ActionResult<InvoicePartnerChangeRes>> SetPartner(
    [FromBody] SetInvoicePartnerReq req
  )
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => partnerValidator.ValidateAsyncResult(req, "Invalid SetInvoicePartnerReq"))
      .ThenAwait(r => settingsRepo.AddPartner(r.ToDomain(), r.EffectiveAt))
      .Then(c => c.ToRes(), Errors.MapAll);
    return this.ReturnResult(x);
  }
}
