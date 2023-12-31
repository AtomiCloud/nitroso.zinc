using CSharp_Result;
using Domain.Timings;

namespace Domain.Booking;

public interface IBookingService
{
  Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search);

  Task<Result<Booking?>> Get(string? userId, Guid id);

  Task<Result<BookingPrincipal>> Create(string userId, decimal cost, BookingRecord record);

  Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingRecord record);

  Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly time);

  Task<Result<BookingPrincipal?>> Buying(Guid id);

  Task<Result<BookingPrincipal?>> Complete(Guid id, Stream ticketFile);

  Task<Result<BookingPrincipal?>> Cancel(string? userId, Guid id);

  Task<Result<BookingPrincipal?>> Terminate(string? userId, Guid id);

  Task<Result<BookingPrincipal?>> Refund(Guid id);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  Task<Result<IEnumerable<BookingCount>>> Count();
}
