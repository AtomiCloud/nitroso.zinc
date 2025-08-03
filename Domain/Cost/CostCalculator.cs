using CSharp_Result;
using Domain.Booking;

namespace Domain.Cost;

public class CostCalculator(ICostService service) : ICostCalculator
{
  public Task<Result<decimal>> BookingCost(string userId, string[] roles, BookingRecord record)
  {
    return service.Materialize(userId, roles).Then(x => x.Final, Errors.MapNone);
  }
}
