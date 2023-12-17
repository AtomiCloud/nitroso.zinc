using CSharp_Result;

namespace Domain.Booking;

public interface IBookingRepository
{
  Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search);

  Task<Result<Booking?>> Get(string? userId, Guid id);

  Task<Result<BookingPrincipal>> Create(string userId, BookingRecord record);

  Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingStatus? status, BookingRecord? record, BookingComplete? complete);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  Task<Result<IEnumerable<BookingCount>>> Count(DateOnly date, TimeOnly time);
}
