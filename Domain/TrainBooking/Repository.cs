using CSharp_Result;

namespace Domain.TrainBooking;

public interface ITrainBookingRepository
{
  Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search);

  Task<Result<Booking?>> Get(string? userId, Guid id);

  Task<Result<BookingPrincipal>> Create(string? userId, BookingRecord record);

  Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingStatus? status, BookingRecord? record);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  // for pollers to get what days they need to poll
  Task<Result<IEnumerable<BookingPoll>>> PollSegment(DateOnly fromDate, TimeOnly fromTime);
}
