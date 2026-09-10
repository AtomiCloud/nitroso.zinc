using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.Modules.Invoices.Data;
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
  PreviewInvoiceReqValidator previewValidator,
  SaveInvoiceDraftReqValidator draftValidator,
  VoidInvoiceReqValidator voidValidator,
  IInvoiceDocumentRepository docRepo,
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

  // Compute a month WITHOUT persisting anything. The operator assembles the
  // input (from GET inputs, GET settings and the pieces zinc does not yet
  // gather), sends it here, and sees the payable before anything is issued.
  //
  // Takes the whole input rather than a month string on purpose: reviewing a
  // draft means changing a figure and watching what it does to the split. A
  // month-string endpoint could only ever show one answer.
  //
  // The arithmetic here is pinned against the issued June, July and August
  // invoices to the cent — see UnitTest/Invoices. Nothing about this endpoint
  // reads or writes the database.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("preview")]
  public async Task<ActionResult<InvoiceComputedRes>> Preview([FromBody] PreviewInvoiceReq req)
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => previewValidator.ValidateAsyncResult(req, "Invalid PreviewInvoiceReq"))
      .Then(r => InvoiceCalculator.Compute(r.ToDomain()).ToRes(), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // ---- stored invoices ----
  //
  // The freeze boundary. Everything above computes; everything below is about
  // a document that was, or will be, sent to the partners.

  // Every month on record, newest first. No frozen payloads — see
  // InvoiceDocumentRepository.List.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet]
  public async Task<ActionResult<IEnumerable<InvoiceSummaryRes>>> List()
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => docRepo.List())
      .Then(rows => rows.Select(r => r.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // One invoice, with both frozen halves.
  //
  // For an Issued invoice the figures returned are read out of storage. The
  // calculator is NOT called, so this reports what was actually paid rather
  // than what today's engine would say — which is the entire point of the
  // freeze (see Domain/Invoice/InvoiceDocument.cs).
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("{id:guid}")]
  public async Task<ActionResult<InvoiceDocumentRes>> Get(Guid id)
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => docRepo.Get(id))
      .Then(doc => doc.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Invoice not found", typeof(InvoiceDocument), id.ToString())
    );
  }

  // Save (or replace) the draft for a month. Computes on the way in and
  // stores both halves, so opening the draft again does not recompute it.
  //
  // Refuses a month that already has an issued invoice — void it first. That
  // check is here rather than at issue time so the operator is told before
  // doing the work, not after.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("drafts")]
  public async Task<ActionResult<InvoiceDocumentRes>> SaveDraft([FromBody] SaveInvoiceDraftReq req)
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => draftValidator.ValidateAsyncResult(req, "Invalid SaveInvoiceDraftReq"))
      .ThenAwait(r => docRepo.SaveDraft(r.ToDomain(), this.Sub()))
      .Then(doc => doc.ToRes()!, Errors.MapAll);
    return this.ReturnResult(x);
  }

  // Freeze a draft. The figures are NOT recomputed here: what the operator
  // reviewed is what gets issued, even if a booking moved in between.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("{id:guid}/issue")]
  public async Task<ActionResult<InvoiceDocumentRes>> Issue(Guid id)
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => docRepo.Issue(id, this.Sub()))
      .Then(doc => doc.ToRes()!, Errors.MapAll);
    return this.ReturnResult(x);
  }

  // Withdraw an issued invoice. The row and both frozen halves stay exactly
  // as they are — only the status and the void fields change, because an
  // invoice that was sent and then retracted is part of the record and so is
  // what it said.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("{id:guid}/void")]
  public async Task<ActionResult<InvoiceDocumentRes>> Void(Guid id, [FromBody] VoidInvoiceReq req)
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => voidValidator.ValidateAsyncResult(req, "Invalid VoidInvoiceReq"))
      .ThenAwait(r => docRepo.Void(id, r.Reason, this.Sub()))
      .Then(doc => doc.ToRes()!, Errors.MapAll);
    return this.ReturnResult(x);
  }

  // What today's engine would compute over this invoice's frozen inputs, and
  // where that differs from what it paid.
  //
  // A REPORT, never a correction. Freezing hides engine bugs by design — an
  // issued invoice renders from stored figures and never calls the calculator
  // — so this is the only thing that makes a later engine fix visible on the
  // documents already sent. What to do about a difference (reissue, credit
  // note, leave it) is a human decision; this project has already had one
  // where "leave it" was the right answer.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("{id:guid}/drift")]
  public async Task<ActionResult<InvoiceDriftRes>> Drift(Guid id)
  {
    var x = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => docRepo.Get(id))
      .Then(
        doc =>
          doc is null
            ? null
            : InvoiceDriftCheck
              .Compare(doc.Id, doc.Record.EngineVersion, doc.Figures, doc.ToInputs())
              .ToRes(),
        Errors.MapAll
      );
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Invoice not found", typeof(InvoiceDocument), id.ToString())
    );
  }
}
