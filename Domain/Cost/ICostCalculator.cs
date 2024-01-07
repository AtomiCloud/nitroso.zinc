using CSharp_Result;
using Domain.Booking;

namespace Domain.Cost;

public interface ICostCalculator
{
  Task<Result<decimal>> BookingCost(string userId, string[] roles, BookingRecord record);
}
