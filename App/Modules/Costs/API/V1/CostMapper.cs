using App.Modules.Discounts.API.V1;
using App.Modules.Timings.API.V1;
using App.Utility;
using Domain.Booking;
using Domain.Cost;

namespace App.Modules.Costs.API.V1;

public static class CostMapper
{
  // Domain -> RES
  public static CostPrincipalRes ToRes(this CostPrincipal principal) =>
    new(principal.Id, principal.CreatedAt, principal.Record.Cost);

  public static CostPolicyLineRes ToRes(this CostPolicyLine line) => new(line.Name, line.Delta);

  public static MaterializedCostRes ToRes(this MaterializedCost cost) =>
    new(
      cost.Cost,
      cost.PolicyLines.Select(x => x.ToRes()).ToArray(),
      cost.Subtotal,
      cost.Final,
      cost.Discounts.Select(x => x.ToRes()).ToArray()
    );

  public static CostSummaryRes ToSummaryRes(this MaterializedCost cost) =>
    new(
      cost.Cost,
      cost.PolicyLines.Select(x => x.ToRes()).ToArray(),
      cost.Subtotal,
      cost.Discounts.Select(x => x.ToRes()).ToArray(),
      cost.Final,
      BookingPriceQuote.Create(cost.Final)
    );

  public static CostSlotSummaryRes ToRes(this MaterializedCostSlot slot) =>
    new(
      slot.Time.ToStandardTimeFormat(),
      slot.Cost.Cost,
      slot.Cost.PolicyLines.Select(x => x.ToRes()).ToArray(),
      slot.Cost.Subtotal,
      slot.Cost.Discounts.Select(x => x.ToRes()).ToArray(),
      slot.Cost.Final,
      BookingPriceQuote.Create(slot.Cost.Final)
    );

  public static CostPolicyPrincipalRes ToRes(this CostPolicyPrincipal principal) =>
    new(
      principal.Id,
      principal.CreatedAt,
      principal.Record.Name,
      principal.Record.Enabled,
      principal.Record.MatchDate?.ToStandardDateFormat(),
      principal.Record.MatchTime?.ToStandardTimeFormat(),
      principal.Record.MatchDayOfWeek?.ToString(),
      principal.Record.MatchDirection?.ToRes(),
      principal.Record.LeadTimeUnderHours,
      principal.Record.Amount,
      principal.Record.IsPercentage,
      principal.Record.EffectiveAt,
      principal.Record.ExpiresAt
    );

  // REQ -> Domain
  public static CostRecord ToDomain(this CreateCostReq req) => new() { Cost = req.Cost };

  public static CostPolicyRecord ToDomain(this CostPolicyReq req) =>
    new()
    {
      Name = req.Name,
      Enabled = req.Enabled,
      MatchDate = req.MatchDate?.ToDate(),
      MatchTime = req.MatchTime?.ToTime(),
      MatchDayOfWeek = req.MatchDayOfWeek == null
        ? null
        : Enum.Parse<DayOfWeek>(req.MatchDayOfWeek),
      MatchDirection = req.MatchDirection?.DirectionToDomain(),
      LeadTimeUnderHours = req.LeadTimeUnderHours,
      Amount = req.Amount,
      IsPercentage = req.IsPercentage,
      EffectiveAt = req.EffectiveAt,
      ExpiresAt = req.ExpiresAt,
    };

  public static BookingCostSpec ToDomain(this CostSummaryQuery query) =>
    new()
    {
      Date = query.Date.ToDate(),
      Time = query.Time.ToTime(),
      Direction = query.Direction.DirectionToDomain(),
    };

  public static TimeOnly[] ToTimes(this CostSummaryBatchQuery query) =>
    query.Times.Split(',').Select(x => x.ToTime()).ToArray();
}
