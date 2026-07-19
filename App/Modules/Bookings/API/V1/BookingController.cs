using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.Modules.Timings.API.V1;
using App.StartUp.Options;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain;
using Domain.Booking;
using Domain.Cost;
using Domain.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Utils = App.Utility.Utils;

namespace App.Modules.Bookings.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class BookingController(
  IBookingService service,
  ICostCalculator costCalculator,
  CreateBookingReqValidator createBookingReqValidator,
  BookingSearchQueryValidator bookingSearchQueryValidator,
  ReserveBookingQueryValidator reserveBookingQueryValidator,
  BookingCountQueryValidator countQueryValidator,
  SetPrioritySettingsReqValidator setPrioritySettingsReqValidator,
  PriorityEligibilityQueryValidator priorityEligibilityQueryValidator,
  IPrioritySettingsRepository prioritySettingsRepo,
  IPriorityAccessRepository priorityAccessRepo,
  IBookingAnalysisRepository analysisRepo,
  IKtmbCostRepository ktmbCostRepo,
  IKtmbFxRateRepository ktmbFxRateRepo,
  IUserService userService,
  IOptions<TerminatorOption> terminatorOptions,
  IOptions<RecoveryOption> recoveryOptions,
  ILogger<BookingController> logger,
  IBookingImageEnricher enrich,
  IAuthHelper helper
) : AtomiControllerBase(helper)
{
  [Authorize, HttpGet]
  public async Task<ActionResult<IEnumerable<BookingPrincipalRes>>> Search(
    [FromQuery] SearchBookingQuery query
  )
  {
    var x = await this.GuardOrAnyAsync(
        query.UserId,
        AuthRoles.Field,
        AuthRoles.Admin,
        AuthRoles.Tin
      )
      .ThenAwait(_ =>
        bookingSearchQueryValidator.ValidateAsyncResult(query, "Invalid SearchBookingQuery")
      )
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapAll)
      .ThenAwait(x => enrich.Enrich(x));

    return this.ReturnResult(x);
  }

  // total matches for the same filters — lets the UI show real page numbers
  // and jump to any page
  [Authorize, HttpGet("search/count")]
  public async Task<ActionResult<SearchCountRes>> SearchCount([FromQuery] SearchBookingQuery query)
  {
    var x = await this.GuardOrAnyAsync(
        query.UserId,
        AuthRoles.Field,
        AuthRoles.Admin,
        AuthRoles.Tin
      )
      .ThenAwait(_ =>
        bookingSearchQueryValidator.ValidateAsyncResult(query, "Invalid SearchBookingQuery")
      )
      .ThenAwait(q => service.SearchCount(q.ToDomain()))
      .Then(total => new SearchCountRes(total), Errors.MapNone);

    return this.ReturnResult(x);
  }

  // where this booking sits in its timeslot's purchase queue (1 = next to be
  // bought); users may only see their own bookings
  [Authorize, HttpGet("{id:guid}/queue")]
  public async Task<ActionResult<BookingQueuePositionRes>> QueuePosition(Guid id, string? userId)
  {
    var x = await this.GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin, AuthRoles.Tin)
      .ThenAwait(_ => service.QueuePosition(userId, id))
      .Then(p => p?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // booking outcomes grouped by travel day-of-week, departure time, direction
  // and purchase-to-departure lead time — the success-rate dashboard source
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("stats")]
  public async Task<ActionResult<IEnumerable<BookingStatRes>>> Stats(
    [FromQuery] BookingStatsQueryReq query,
    [FromServices] BookingStatsQueryReqValidator statsValidator
  )
  {
    var x = await statsValidator
      .ValidateAsyncResult(query, "Invalid BookingStatsQueryReq")
      .ThenAwait(q => service.Stats(q.ToDomain()))
      .Then(rows => rows.Select(r => r.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // Sales/revenue analysis for the admin Analysis page: per SGT-day rows of
  // completed tickets, gross revenue and KTMB cost; a range summary
  // (deposits, internal fees, synced gateway fees, per-direction split); a
  // monthly rollup (net = gross − ktmbCost − gateway fees; internal fees are
  // revenue and excluded from the cost side); and revenue-by-component over
  // the persisted purchase price breakdowns.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("analysis")]
  public async Task<ActionResult<BookingAnalysisRes>> Analysis(
    [FromQuery] BookingAnalysisQueryReq query,
    [FromServices] BookingAnalysisQueryReqValidator analysisValidator
  )
  {
    // non-owners only see history from RangeClamp.NonOwnerFloor onward — a
    // clamp, not an error (same gate on every analysis/P&L endpoint)
    var x = await analysisValidator
      .ValidateAsyncResult(query, "Invalid BookingAnalysisQueryReq")
      .ThenAwait(q =>
        analysisRepo.Analyze(q.ToDomain().ClampHistory(this.HasRoleIgnoreCase(AuthRoles.Owner)))
      )
      .Then(a => a.ToRes(), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // The travel-date demand view for the admin Analysis page: how many
  // tickets are secured FOR each travel date (b.Date, not CompletedAt), per
  // direction, collapsed into quarter-day (6-hour) departure buckets — only
  // buckets with completed tickets are returned, ordered by date, direction,
  // bucket. After/Before are inclusive travel dates; null = unbounded.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("analysis/travel")]
  public async Task<ActionResult<IEnumerable<BookingTravelAnalysisRowRes>>> TravelAnalysis(
    [FromQuery] BookingTravelAnalysisQueryReq query,
    [FromServices] BookingTravelAnalysisQueryReqValidator travelValidator
  )
  {
    var x = await travelValidator
      .ValidateAsyncResult(query, "Invalid BookingTravelAnalysisQueryReq")
      .ThenAwait(q =>
        analysisRepo.TravelAnalysis(
          q.ToDomain().ClampHistory(this.HasRoleIgnoreCase(AuthRoles.Owner))
        )
      )
      .Then(rows => rows.Select(r => r.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // The travel-day profit view for the admin Analysis page: revenue vs
  // ACTUAL KTMB cost (SGD; no estimate fallback) per travel date (b.Date,
  // not CompletedAt) and quarter-day (6-hour) departure bucket, BOTH
  // directions combined — withActualCost reports cost coverage and the
  // client computes profit = revenue − cost. Only blocks with completed
  // tickets are returned, ordered by date then bucket. After/Before are
  // inclusive travel dates; null = unbounded.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("analysis/profit")]
  public async Task<ActionResult<IEnumerable<BookingProfitAnalysisRowRes>>> ProfitAnalysis(
    [FromQuery] BookingProfitAnalysisQueryReq query,
    [FromServices] BookingProfitAnalysisQueryReqValidator profitValidator
  )
  {
    var x = await profitValidator
      .ValidateAsyncResult(query, "Invalid BookingProfitAnalysisQueryReq")
      .ThenAwait(q =>
        analysisRepo.ProfitAnalysis(
          q.ToDomain().ClampHistory(this.HasRoleIgnoreCase(AuthRoles.Owner))
        )
      )
      .Then(rows => rows.Select(r => r.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // Monthly P&L for the admin Analysis page. Every source is bucketed on its
  // SGT calendar month; empty months are omitted and the client derives nets.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("analysis/pnl")]
  public async Task<ActionResult<IEnumerable<BookingPnlAnalysisRowRes>>> PnlAnalysis(
    [FromQuery] BookingPnlAnalysisQueryReq query,
    [FromServices] BookingPnlAnalysisQueryReqValidator pnlValidator
  )
  {
    var x = await pnlValidator
      .ValidateAsyncResult(query, "Invalid BookingPnlAnalysisQueryReq")
      .ThenAwait(q =>
        analysisRepo.PnlAnalysis(q.ToDomain().ClampHistory(this.HasRoleIgnoreCase(AuthRoles.Owner)))
      )
      .Then(rows => rows.Select(r => r.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // Terminal-event P&L for the admin P&L page: profit is recognized when
  // money reaches a terminal state — a completed booking, a terminated
  // booking or a completed withdrawal. Each month reports its deposits and
  // payment gateway fees so every deposited dollar carries its gateway-fee
  // share to its terminal event via the month's blended rate (gwRate,
  // server-computed at 6dp); the client owns all profit formulas. Sources
  // bucket on their SGT calendar month (payment CreatedAt,
  // booking/withdrawal CompletedAt, gateway-fee TransactedAt); empty months
  // are omitted, rows sort month-ascending.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("pnl/terminal")]
  public async Task<ActionResult<IEnumerable<BookingPnlTerminalRowRes>>> PnlTerminal(
    [FromQuery] BookingPnlTerminalQueryReq query,
    [FromServices] BookingPnlTerminalQueryReqValidator pnlTerminalValidator
  )
  {
    var x = await pnlTerminalValidator
      .ValidateAsyncResult(query, "Invalid BookingPnlTerminalQueryReq")
      .ThenAwait(q =>
        analysisRepo.PnlTerminal(q.ToDomain().ClampHistory(this.HasRoleIgnoreCase(AuthRoles.Owner)))
      )
      .Then(rows => rows.Select(r => r.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // The boost ledger: who prioritized which booking, when, for what fee (or
  // free). BoostedAt falls back to the booking's CreatedAt for boosts that
  // predate the PrioritizedAt stamp; GrantedBy identifies admin-granted
  // boosts from this release onward (null before, and for self-boosts).
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("analysis/boosts")]
  public async Task<ActionResult<BookingBoostPageRes>> Boosts(
    [FromQuery] BookingBoostQueryReq query,
    [FromServices] BookingBoostQueryReqValidator boostValidator
  )
  {
    var x = await boostValidator
      .ValidateAsyncResult(query, "Invalid BookingBoostQueryReq")
      .ThenAwait(q =>
        analysisRepo.Boosts(q.ToDomain().ClampHistory(this.HasRoleIgnoreCase(AuthRoles.Owner)))
      )
      .Then(p => p.ToRes(), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // the KTMB ticket cost in effect right now per direction + queued future
  // changes (analysis costing source; 0 = never configured)
  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpGet("ktmb-cost/current")]
  public async Task<ActionResult<KtmbCostRes>> GetKtmbCost()
  {
    var x = await ktmbCostRepo
      .List()
      .Then(changes => KtmbCostSchedule.View(changes, DateTime.UtcNow).ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // queue a per-direction KTMB cost change (immediate when EffectiveAt is
  // omitted) — insert-only, effective-dated like the withdrawal Fee queue
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("ktmb-cost")]
  public async Task<ActionResult<KtmbCostChangeRes>> SetKtmbCost(
    [FromBody] SetKtmbCostReq req,
    [FromServices] SetKtmbCostReqValidator ktmbCostValidator
  )
  {
    var x = await ktmbCostValidator
      .ValidateAsyncResult(req, "Invalid SetKtmbCostReq")
      .ThenAwait(r =>
        ktmbCostRepo.Add(r.Direction.DirectionToDomain(), r.Cost, r.EffectiveAt)
      )
      .Then(c => c.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // the MYR -> SGD rate row in effect right now (null before any row) + the
  // entered rows most recent first — the actual-KTMB-cost conversion source
  // for the analysis
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("ktmb-fx")]
  public async Task<ActionResult<KtmbFxRateRes>> GetKtmbFxRate()
  {
    var x = await ktmbFxRateRepo
      .List()
      .Then(
        changes => KtmbFxRateSchedule.View(changes, DateTime.UtcNow).ToRes(),
        Errors.MapNone
      );
    return this.ReturnResult(x);
  }

  // queue an MYR -> SGD rate change (immediate when EffectiveAt is omitted)
  // — insert-only, effective-dated like the KTMB cost queue
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("ktmb-fx")]
  public async Task<ActionResult<KtmbFxRateRowRes>> SetKtmbFxRate(
    [FromBody] SetKtmbFxRateReq req,
    [FromServices] SetKtmbFxRateReqValidator ktmbFxRateValidator
  )
  {
    var x = await ktmbFxRateValidator
      .ValidateAsyncResult(req, "Invalid SetKtmbFxRateReq")
      .ThenAwait(r => ktmbFxRateRepo.Add(r.Rate, r.EffectiveAt))
      .Then(c => c.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpGet("refund")]
  public async Task<ActionResult<IEnumerable<BookingPrincipalRes>>> ListRefunds()
  {
    var x = await service
      .ListRefunds(DateTime.UtcNow.Add(TimeSpan.FromMinutes(terminatorOptions.Value.MinBuffer)))
      .Then(x => x.Select(u => u.ToRes()), Errors.MapAll)
      .ThenAwait(enrich.Enrich);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpPost("recovering/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Recovering(Guid id)
  {
    var x = await service.Recovering(id).Then(x => x?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // Recycle a 'Recovering' booking back to 'Pending' for another purchase
  // attempt, incrementing its RecoveryRetries counter. Status-only, no money
  // moves. Refused with 409 recovery_retries_exhausted once the booking has
  // been recycled Recovery:MaxRetries times — then a human resolves it via
  // duplicate/ or manual-intervention/.
  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpPost("recover-revert/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> RecoverRevert(Guid id)
  {
    var x = await service
      .RecoverRevert(id, recoveryOptions.Value.MaxRetries)
      .Then(x => x?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpPost("duplicate/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Duplicate(Guid id)
  {
    var x = await service.Duplicate(id).Then(x => x?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpPost("manual-intervention/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> ManualIntervention(Guid id)
  {
    var x = await service.ManualIntervention(id).Then(x => x?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpGet("reserve/{Direction}/{Date}/{Time}")]
  public async Task<ActionResult<BookingPrincipalRes>> Reserve(
    [FromRoute] ReserveBookingQuery query
  )
  {
    var book = await reserveBookingQueryValidator
      .ValidateAsyncResult(query, "Invalid ReserveBookingQuery")
      .ThenAwait(q =>
        service.Reserve(q.Direction.DirectionToDomain(), q.Date.ToDate(), q.Time.ToTime())
      )
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(
      book,
      new EntityNotFound(
        "Booking not found",
        typeof(Booking),
        $"{query.Direction}-{query.Date}-{query.Time}"
      )
    );
  }

  [Authorize, HttpGet("{id:guid}")]
  public async Task<ActionResult<BookingRes>> Get(Guid id, string? userId)
  {
    var x = await this.GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin, AuthRoles.Tin)
      .ThenAwait(_ => service.Get(userId, id))
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // ktmbAmount/ktmbCurrency (optional, both or neither): what tin actually
  // paid KTMB for the ticket — stored on the completion so the analysis can
  // cost the booking at the real price instead of the admin estimate
  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpPost("complete/{id:guid}")]
  [Consumes(MediaTypeNames.Multipart.FormData)]
  public async Task<ActionResult<BookingPrincipalRes>> Complete(
    Guid id,
    string bookingNo,
    string ticketNo,
    IFormFile file,
    [FromServices] CompleteKtmbCostReqValidator ktmbCostValidator,
    decimal? ktmbAmount,
    string? ktmbCurrency
  )
  {
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    logger.LogInformation("Stream Size: {StreamSize}", stream.Length);
    var x = await ktmbCostValidator
      .ValidateAsyncResult(
        new CompleteKtmbCostReq(ktmbAmount, ktmbCurrency),
        "Invalid CompleteKtmbCostReq"
      )
      .ThenAwait(k => service.Complete(id, bookingNo, ticketNo, stream, k.ToDomain()))
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // The tin backfill worklist: bookings of the queried status (default
  // Completed; Terminated for terminated-then-refunded history) that
  // captured a KTMB reservation (BookingNo + TicketNo present) but have no
  // actual paid amount recorded yet, oldest CompletedAt first
  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpGet("ktmb-cost/missing")]
  public async Task<ActionResult<IEnumerable<KtmbCostMissingRes>>> MissingKtmbCost(
    [FromQuery] KtmbCostMissingQuery query,
    [FromServices] KtmbCostMissingQueryValidator missingValidator
  )
  {
    var x = await missingValidator
      .ValidateAsyncResult(query, "Invalid KtmbCostMissingQuery")
      .ThenAwait(q =>
        service.ListMissingKtmbActualCost(
          (BookStatus)(q.Status ?? (int)BookStatus.Completed),
          q.Limit ?? 100,
          q.Skip ?? 0
        )
      )
      .Then(rows => rows.Select(m => m.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // The tin refund-backfill worklist: Terminated bookings with a recorded
  // actual KTMB cost but no captured KTMB termination refund yet, oldest
  // CompletedAt first (same row shape as ktmb-cost/missing)
  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpGet("ktmb-refund/missing")]
  public async Task<ActionResult<IEnumerable<KtmbCostMissingRes>>> MissingKtmbRefund(
    [FromQuery] KtmbRefundMissingQuery query,
    [FromServices] KtmbRefundMissingQueryValidator refundMissingValidator
  )
  {
    var x = await refundMissingValidator
      .ValidateAsyncResult(query, "Invalid KtmbRefundMissingQuery")
      .ThenAwait(q => service.ListMissingKtmbRefund(q.Limit ?? 100, q.Skip ?? 0))
      .Then(rows => rows.Select(m => m.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // Record the actual KTMB-paid cost of an already-Completed booking after
  // the fact (the tin backfill). Idempotent: once recorded, the stored
  // values are returned as-is and never overwritten.
  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("{id:guid}/ktmb-cost")]
  public async Task<ActionResult<BookingKtmbCostRes>> RecordKtmbCost(
    Guid id,
    [FromBody] SetBookingKtmbCostReq req,
    [FromServices] SetBookingKtmbCostReqValidator ktmbActualValidator
  )
  {
    var x = await ktmbActualValidator
      .ValidateAsyncResult(req, "Invalid SetBookingKtmbCostReq")
      .ThenAwait(r => service.RecordKtmbActualCost(id, r.ToDomain()))
      .Then(c => c?.ToRes(id), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // Capture the actual KTMB termination refund of a booking (tin's
  // terminator sends KTMB's GetRefundPolicy amount; the backfill records it
  // for historical terminations). Upsert: a repeat call overwrites.
  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("{id:guid}/ktmb-refund")]
  public async Task<ActionResult<BookingKtmbRefundRes>> RecordKtmbRefund(
    Guid id,
    [FromBody] SetBookingKtmbRefundReq req,
    [FromServices] SetBookingKtmbRefundReqValidator ktmbRefundValidator
  )
  {
    var x = await ktmbRefundValidator
      .ValidateAsyncResult(req, "Invalid SetBookingKtmbRefundReq")
      .ThenAwait(r => service.RecordKtmbRefund(id, r.ToDomain()))
      .Then(r => r?.ToRes(id), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // Complete a RequireManualIntervention booking with an admin-supplied ticket
  // but WITHOUT collecting the money — the held BookingReserve is left untouched
  // for manual settlement later.
  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpPost("complete-no-collect/{id:guid}")]
  [Consumes(MediaTypeNames.Multipart.FormData)]
  public async Task<ActionResult<BookingPrincipalRes>> CompleteNoCollect(
    Guid id,
    string bookingNo,
    string ticketNo,
    IFormFile file
  )
  {
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    logger.LogInformation("Stream Size: {StreamSize}", stream.Length);
    var x = await service
      .CompleteNoCollect(id, bookingNo, ticketNo, stream)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // Repair the ticket artefacts of an already-Completed booking: replace the
  // ticket file reference (dangling-ref repair) and backfill missing booking/
  // ticket numbers. No money moves and no status change; conflicting non-null
  // identifiers are rejected.
  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpPost("ticket/{id:guid}")]
  [Consumes(MediaTypeNames.Multipart.FormData)]
  public async Task<ActionResult<BookingPrincipalRes>> AttachTicket(
    Guid id,
    IFormFile file,
    string? bookingNo = null,
    string? ticketNo = null
  )
  {
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    logger.LogInformation("Stream Size: {StreamSize}", stream.Length);
    var x = await service
      .AttachTicket(id, bookingNo, ticketNo, stream)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // Cheap single-booking probe of the ticket reference: hasRef = the booking
  // carries a ticket key, refValid = that key resolves to a real object in
  // block storage. Surfaces dangling references without serving the object.
  [Authorize(Policy = AuthPolicies.AdminOrTin)]
  [HttpGet("ticket-health/{id:guid}")]
  public async Task<ActionResult<BookingTicketHealthRes>> TicketHealth(Guid id)
  {
    var x = await service.TicketHealth(id).Then(h => h?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpGet("counts")]
  public async Task<ActionResult<IEnumerable<BookingCountRes>>> CountStatus()
  {
    var x = await service.Count().Then(x => x.Select(c => c.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  [Authorize, HttpGet("counts/{Direction}/{Date}")]
  public async Task<ActionResult<IEnumerable<BookingCountRes>>> CountStatus(
    [FromRoute] BookingCountQuery query
  )
  {
    var x = await countQueryValidator
      .ValidateAsyncResult(query, "Invalid BookingCountQuery")
      .ThenAwait(q => service.Count(q.ToDomain()))
      .Then(x => x.Select(c => c.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("buying/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Buying(Guid id)
  {
    var x = await service
      .Buying(id)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // Atomically revert a stuck Buying booking back to Pending (guarded Buying-only
  // + uncaptured in the service). Used to recycle bookings that failed for a
  // transient reason such as an insufficient KTMB wallet balance.
  // force=false (default, used by the reverter cron) additionally requires the
  // booking to have been in Buying for over 5 minutes; force=true (admin UI)
  // skips the age check and also accepts RequireManualIntervention.
  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("revert/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Revert(Guid id, [FromQuery] bool force = false)
  {
    var x = await service
      .Revert(id, force)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  [Authorize(Policy = AuthPolicies.AdminOrTin), HttpPost("refund/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Refund(Guid id)
  {
    var x = await service
      .Refund(id)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .ThenAwait(x => Utils.ToNullableTaskResultOr(x, r => enrich.Enrich(r)));
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // Purchase
  [Authorize, HttpPost("{userId}/purchase")]
  public async Task<ActionResult<BookingPrincipalRes>> Purchase(
    string userId,
    [FromBody] CreateBookingReq req
  )
  {
    var p = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ =>
        createBookingReqValidator.ValidateAsyncResult(
          req.Normalize(),
          "Failed to validate CreateBookingReq"
        )
      )
      .Then(r => r.ToRecord(), Errors.MapNone)
      // Reject an expired/cutoff slot before user lookup or pricing. The
      // domain Create boundary repeats this immediately before wallet work.
      .Then(r => BookingPurchaseTiming.Validate(r!, DateTimeOffset.UtcNow), Errors.MapNone)
      // PRICING roles = the TARGET user's roles ∪ their admin-granted
      // ExtraRoles — the SAME union the Cost summary endpoints compute for
      // that user, so the price previewed is the price charged (an
      // extra-role-targeted discount must not vanish at purchase, and an
      // admin purchasing for X must not price with the admin's own roles).
      // Self-purchase uses the caller's JWT roles exactly like Cost/summary;
      // admin-assisted falls back to X's persisted Descope-synced Roles.
      .ThenAwait(rec =>
        userService
          .GetById(userId)
          .Then(
            u =>
              PurchasePricingRoles.For(
                this.Sub() == userId,
                helper.FieldToScope(this.HttpContext.User, AuthRoles.Field),
                u?.Principal.Record
              ),
            Errors.MapNone
          )
          .ThenAwait(roles =>
            costCalculator
              .BookingCostDetail(userId, roles, rec!)
              .Then(cost =>
              {
                // Optional for one release: old (raichu) argon clients don't
                // send the quote yet — charge the server-computed price and
                // tighten to required after raichu argon ships the quote.
                if (req.ExpectedCost == null)
                  logger.LogWarning(
                    "Purchase without confirmed quote (legacy client): User '{UserId}'",
                    userId
                  );
                return BookingPriceQuote
                  .Validate(cost.Final, req.ExpectedCost)
                  .Then(_ => cost, Errors.MapNone);
              })
              .Then(cost => (c: cost, r: rec!), Errors.MapNone)
          )
      )
      // the charged price AND its composition — the breakdown is persisted
      // on the booking so component analytics can rank policies/discounts
      .ThenAwait(cr => service.Create(userId, cr.c.Final, cr.r!, cr.c.ToBreakdown()))
      .Then(b => b.ToRes(), Errors.MapNone);

    return this.ReturnResult(p);
  }

  // cancel
  [HttpPost("cancel/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Cancel(Guid id, string? userId)
  {
    var p = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(cr => service.Cancel(userId, id))
      .Then(b => b?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(
      p,
      new EntityNotFound(
        "Cannot find booking to be cancelled",
        typeof(BookingPrincipal),
        id.ToString()
      )
    );
  }

  // terminate
  [HttpPost("terminate/{id:guid}")]
  public async Task<ActionResult<BookingPrincipalRes>> Terminate(Guid id, string? userId)
  {
    var p = await this.GuardOrAllAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(cr =>
        service.Terminate(
          userId,
          id,
          DateTime.UtcNow.Add(TimeSpan.FromMinutes(terminatorOptions.Value.MinBuffer))
        )
      )
      .Then(b => b?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(
      p,
      new EntityNotFound(
        "Cannot find booking to be terminated",
        typeof(BookingPrincipal),
        id.ToString()
      )
    );
  }

  // may the CALLING user prioritize a booking right now, at what fee, and
  // free for them — powers the prioritize button on the booking page and the
  // boost opt-in on the purchase page. The purchase page sends the slot it
  // is buying (direction/date/time, Reserve-route formats, all or none) so
  // hour-bounded policies and the slot cap apply before any booking exists;
  // without one the answer only reflects rules that hold for every timeslot.
  // The caller IS the target, so their JWT roles are authoritative for the
  // free/access targeting (∪ ExtraRoles happens in the domain, exactly like
  // pricing)
  [Authorize, HttpGet("priority/eligibility")]
  public async Task<ActionResult<PriorityEligibilityRes>> PriorityEligibility(
    [FromQuery] PriorityEligibilityQuery query
  )
  {
    var sub = this.Sub()!;
    var tokenRoles = helper.FieldToScope(this.HttpContext.User, AuthRoles.Field).ToArray();
    var x = await priorityEligibilityQueryValidator
      .ValidateAsyncResult(query, "Invalid PriorityEligibilityQuery")
      .ThenAwait(q => service.PriorityEligibility(sub, tokenRoles, q.ToSlot()))
      .Then(e => e.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // booking-scoped eligibility: hour-bounded policies (hours to the
  // timeslot's departure) and the per-timeslot slot cap apply — powers the
  // prioritize dialog with "N priority slots left". Users may only check
  // their own bookings (same guard as GET {id}/queue); the caller's JWT
  // roles are authoritative for targeting only when they query their own
  // scope (userId set), matching how pricing treats roles
  [Authorize, HttpGet("{id:guid}/priority/eligibility")]
  public async Task<ActionResult<PriorityEligibilityRes>> BookingPriorityEligibility(
    Guid id,
    string? userId
  )
  {
    var tokenRoles =
      userId != null && userId == this.Sub()
        ? helper.FieldToScope(this.HttpContext.User, AuthRoles.Field).ToArray()
        : null;
    var x = await this.GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.BookingPriorityEligibility(userId, id, tokenRoles))
      .Then(e => e?.ToRes(), Errors.MapAll);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("Booking not found", typeof(Booking), id.ToString())
    );
  }

  // the priority settings in effect right now (defaults when never configured)
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("priority/settings")]
  public async Task<ActionResult<PrioritySettingsRes>> GetPrioritySettings()
  {
    var x = await prioritySettingsRepo.GetCurrent().Then(s => s.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // replace the priority settings (insert-only latest, like Cost)
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("priority/settings")]
  public async Task<ActionResult<PrioritySettingsRes>> SetPrioritySettings(
    [FromBody] SetPrioritySettingsReq req
  )
  {
    var x = await setPrioritySettingsReqValidator
      .ValidateAsyncResult(req, "Invalid SetPrioritySettingsReq")
      .ThenAwait(r => prioritySettingsRepo.Create(r.ToDomain()))
      .Then(s => s.Record.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // the per-user priority allowlist
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("priority/access")]
  public async Task<ActionResult<IEnumerable<PriorityAccessRes>>> ListPriorityAccess()
  {
    var x = await priorityAccessRepo.List().Then(a => a.Select(u => u.ToRes()), Errors.MapNone);
    return this.ReturnResult(x);
  }

  // allowlist a user (idempotent)
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("priority/access/{userId}")]
  public async Task<ActionResult<PriorityAccessRes>> AddPriorityAccess(string userId)
  {
    var x = await priorityAccessRepo.Add(userId).Then(a => a.ToRes(), Errors.MapNone);
    return this.ReturnResult(x);
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("priority/access/{userId}")]
  public async Task<ActionResult> RemovePriorityAccess(string userId)
  {
    var x = await priorityAccessRepo.Remove(userId);
    return this.ReturnUnitNullableResult(
      x,
      new EntityNotFound("Priority access not found", typeof(PriorityAccess), userId)
    );
  }

  // prioritize: charge the priority fee and jump this booking to the front of
  // its timeslot's purchase queue (owner or admin, like cancel/terminate)
  [Authorize, HttpPost("{id:guid}/prioritize")]
  public async Task<ActionResult<BookingPrincipalRes>> Prioritize(Guid id, string? userId)
  {
    // the caller's sub is persisted as the granter when it is not the
    // booking's owner (admin-granted boost attribution in the boost ledger)
    var p = await this.GuardOrAnyAsync(userId, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.Prioritize(userId, id, this.Sub()))
      .Then(b => b?.ToRes(), Errors.MapNone);

    return this.ReturnNullableResult(
      p,
      new EntityNotFound(
        "Cannot find booking to be prioritized",
        typeof(BookingPrincipal),
        id.ToString()
      )
    );
  }
}
