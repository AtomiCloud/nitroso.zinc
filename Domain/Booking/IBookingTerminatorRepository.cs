using CSharp_Result;

namespace Domain.Booking;

public interface IBookingTerminatorRepository
{
  Task<Result<Unit>> Terminate(BookingTermination termination);
}
