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
  InvoiceInputQueryReqValidator inputQueryValidator,
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
}
