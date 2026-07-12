using CSharp_Result;
using Domain.Booking;
using Domain.Discount;

namespace Domain.Cost;

public class CostCalculator(ICostService service) : ICostCalculator
{
  public Task<Result<decimal>> BookingCost(string userId, string[] roles, BookingRecord record)
  {
    return this.BookingCostDetail(userId, roles, record).Then(x => x.Final, Errors.MapNone);
  }

  // the full breakdown behind BookingCost, for callers that persist it
  public Task<Result<MaterializedCost>> BookingCostDetail(
    string userId,
    string[] roles,
    BookingRecord record
  )
  {
    // booking-aware: pricing policies match on the booking's date/time/direction
    var spec = new BookingCostSpec
    {
      Date = record.Date,
      Time = record.Time,
      Direction = record.Direction,
    };
    return service.Materialize(userId, roles, spec);
  }
}

// Derives the persisted per-booking price breakdown from a materialized
// price. Discount deltas mirror DiscountCalculator exactly: each discount is
// resolved against the SUBTOTAL (not the running total) — percentage =
// Amount × Subtotal, flat = Amount — and stored negative. The stored lines
// therefore satisfy BaseCost + Σpolicy = Subtotal and Subtotal + Σdiscount ≈
// Final (Final additionally floors at 0 and rounds to numeric(16,8)).
public static class PriceBreakdownFactory
{
  public static BookingPriceBreakdown ToBreakdown(this MaterializedCost cost) =>
    new()
    {
      BaseCost = cost.Cost,
      Lines = cost
        .PolicyLines.Select(l => new BookingPriceLine
        {
          Kind = "policy",
          Name = l.Name,
          Delta = l.Delta,
        })
        .Concat(
          cost.Discounts.Select(d => new BookingPriceLine
          {
            Kind = "discount",
            Name = d.Name,
            Delta = -(d.Type == DiscountType.Percentage ? d.Amount * cost.Subtotal : d.Amount),
          })
        )
        .ToArray(),
      Final = cost.Final,
    };
}
