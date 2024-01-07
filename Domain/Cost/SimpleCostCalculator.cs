using CSharp_Result;
using Domain.Booking;

namespace Domain.Cost;

public class SimpleCostCalculator : ICostCalculator
{
  public Task<Result<decimal>> BookingCost(string userId, string[] roles, BookingRecord record)
  {
    return Task.FromResult((Result<decimal>)10);
  }
}
