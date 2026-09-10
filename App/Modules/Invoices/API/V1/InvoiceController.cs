using System.Net;
using System.Text;
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
  TranscribeInvoiceReqValidator transcribeValidator,
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

  // Record a month invoiced before this system existed.
  //
  // June, July and August went out as PDFs from the invoices/ toolchain. This
  // is how they become rows, so the invoice page can answer "what did we pay
  // last month" instead of the database being a parallel record of the same
  // partnership. The row is written as Issued with EngineVersion 0 — the
  // figures are the document's, not this engine's, and a drift report against
  // them is expected to be non-empty. That is information, not a fault.
  //
  // The transcription is CHECKED, not trusted. The caller states what the
  // document it is holding says was paid, this recomputes from the inputs it
  // supplied alongside, and a disagreement is a 409 listing every figure that
  // differs. Storing a mistyped input as an issued invoice would give a wrong
  // number the strongest claim this system can make about money, and nobody
  // re-reads a settled month. See Domain/Invoice/InvoiceAttestation.cs.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("transcribe")]
  public async Task<ActionResult<InvoiceDocumentRes>> Transcribe(
    [FromBody] TranscribeInvoiceReq req
  )
  {
    var valid = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => transcribeValidator.ValidateAsyncResult(req, "Invalid TranscribeInvoiceReq"));
    if (valid.IsFailure())
      return this.ReturnUnitResult((Result<Unit>)valid.FailureOrDefault());

    var draft = req.ToDomain();
    var computed = InvoiceCalculator.Compute(draft.Inputs);

    var mismatches = InvoiceAttestationCheck.Compare(req.Attest.ToDomain(), computed);
    if (mismatches.Count > 0)
    {
      // 409 rather than 400: the request is well-formed and the figures are
      // internally consistent. What is wrong is that they do not describe the
      // document being transcribed, which is a conflict with reality rather
      // than a malformed payload.
      return this.Error(
        HttpStatusCode.Conflict,
        new EntityConflict(
          "The computed figures do not match the document being transcribed: "
            + string.Join(
              "; ",
              mismatches.Select(m =>
                $"{m.Field} attested {m.Attested} but computes to {m.Computed}"
              )
            ),
          typeof(InvoiceDocument)
        )
      );
    }

    var x = await docRepo
      .Transcribe(draft, computed, req.IssuedAt, this.Sub())
      .Then(doc => doc.ToRes()!, Errors.MapAll);
    return this.ReturnResult(x);
  }

  // ---- the document ----

  // The invoice itself, as print-ready HTML for one partner.
  //
  // Served as text/html rather than a PDF because the browser's own print
  // engine IS the PDF generator that produced the issued documents — same
  // engine, same stylesheet, no new dependency in a financial API. The
  // reasoning is in InvoiceHtml.cs.
  //
  // Rendered from the FROZEN figures. This never calls the calculator, so an
  // invoice that was issued in July still prints July's numbers no matter what
  // the engine does later. Use GET {id}/drift to see whether that matters.
  //
  // Content-Disposition is inline, not attachment: the point is to open it,
  // read it, and press Ctrl+P. A download would put an .html file in the
  // operator's Downloads folder with an extra click before they can see it.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("{id:guid}/document")]
  [ProducesResponseType<string>(StatusCodes.Status200OK, "text/html")]
  public async Task<ActionResult> Document(Guid id, [FromQuery] string suffix)
  {
    var found = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => docRepo.Get(id));
    if (found.IsFailure())
      return this.ReturnUnitResult((Result<Unit>)found.FailureOrDefault());

    var doc = found.SuccessOrDefault();
    if (doc is null)
      return this.Error(
        HttpStatusCode.NotFound,
        new EntityNotFound("Invoice not found", typeof(InvoiceDocument), id.ToString())
      );

    var computed = doc.ToComputed();
    var partner = computed.Result.Shares.FirstOrDefault(s =>
      string.Equals(s.Suffix, suffix, StringComparison.OrdinalIgnoreCase)
    );

    // A suffix that is not on this invoice is a 404 rather than a 400: the
    // partner may genuinely not have been on the partnership that month, and
    // that is a fact about the invoice, not a malformed request.
    if (partner is null)
      return this.Error(
        HttpStatusCode.NotFound,
        new EntityNotFound(
          $"Invoice {id} has no partner with suffix '{suffix}'",
          typeof(InvoiceShare),
          suffix
        )
      );

    return this.Document(computed, partner);
  }

  // The same document for a month that has not been saved yet, so the operator
  // can read the actual invoice before committing to it rather than reviewing
  // a table of figures and hoping.
  //
  // Persists nothing.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("preview/document")]
  [ProducesResponseType<string>(StatusCodes.Status200OK, "text/html")]
  public async Task<ActionResult> PreviewDocument(
    [FromBody] PreviewInvoiceReq req,
    [FromQuery] string? suffix
  )
  {
    var validated = await this.GuardRoleIgnoreCaseAsync(AuthRoles.Owner)
      .ThenAwait(_ => previewValidator.ValidateAsyncResult(req, "Invalid PreviewInvoiceReq"));
    if (validated.IsFailure())
      return this.ReturnUnitResult((Result<Unit>)validated.FailureOrDefault());

    var computed = InvoiceCalculator.Compute(validated.SuccessOrDefault().ToDomain());

    // Suffix is optional here and defaults to the first share. A preview is
    // usually opened to check the shape of the month, not one partner's copy,
    // and every partner's document carries the same workings.
    var partner = string.IsNullOrWhiteSpace(suffix)
      ? computed.Result.Shares.FirstOrDefault()
      : computed.Result.Shares.FirstOrDefault(s =>
        string.Equals(s.Suffix, suffix, StringComparison.OrdinalIgnoreCase)
      );

    if (partner is null)
      return this.Error(
        HttpStatusCode.NotFound,
        new EntityNotFound(
          $"No partner with suffix '{suffix}' in this input",
          typeof(InvoiceShare),
          suffix ?? string.Empty
        )
      );

    return this.Document(computed, partner);
  }

  // Renders and writes the document. Kept in one place so the stored and
  // preview endpoints cannot drift apart in headers or encoding.
  private ContentResult Document(InvoiceComputed computed, InvoiceShare partner)
  {
    this.Response.Headers.ContentDisposition =
      $"inline; filename=\"{InvoiceHtml.FileName(computed, partner)}\"";
    this.Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";

    // An invoice is a statement about a moment. A cached copy shown after a
    // reissue would be a wrong statement, and the operator would have no way
    // to tell.
    this.Response.Headers.CacheControl = "no-store";

    return this.Content(InvoiceHtml.Render(computed, partner), "text/html", Encoding.UTF8);
  }
}
