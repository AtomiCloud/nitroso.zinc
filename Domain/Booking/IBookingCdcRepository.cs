using CSharp_Result;

namespace Domain.Booking;

public interface IBookingCdcRepository
{
  Task<Result<Unit>> Add(string action);
}
