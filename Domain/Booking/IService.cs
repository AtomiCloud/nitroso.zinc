using CSharp_Result;

namespace Domain.Booking;

public interface IBookingService
{
  Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search);

  Task<Result<Booking?>> Get(string? userId, Guid id);

  Task<Result<BookingPrincipal>> Create(string userId, BookingRecord record);

  Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingRecord record);

  Task<Result<BookingPrincipal?>> Buying(Guid id);

  Task<Result<BookingPrincipal?>> Complete(Guid id, Stream ticketFile);

  Task<Result<BookingPrincipal?>> Cancel(Guid id);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  Task<Result<IEnumerable<BookingCount>>> Count();
}
