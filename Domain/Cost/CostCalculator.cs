using CSharp_Result;
using Domain.Booking;

namespace Domain.Cost;

public class CostCalculator(ICostService service) : ICostCalculator
{
  public Task<Result<decimal>> BookingCost(string userId, string[] roles, BookingRecord record)
  {
    // booking-aware: pricing policies match on the booking's date/time/direction
    var spec = new BookingCostSpec
    {
      Date = record.Date,
      Time = record.Time,
      Direction = record.Direction,
    };
    return service.Materialize(userId, roles, spec).Then(x => x.Final, Errors.MapNone);
  }
}
