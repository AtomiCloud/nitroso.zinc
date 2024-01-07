using CSharp_Result;
using Domain.Timings;

namespace Domain.Booking;

public interface IBookingRepository
{
  Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search);

  Task<Result<Booking?>> Get(string? userId, Guid id);

  Task<Result<BookingPrincipal>> Create(string userId, Guid transactionId, BookingRecord record);

  Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingStatus? status, BookingRecord? record,
    BookingComplete? complete);

  Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly Time);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  Task<Result<IEnumerable<BookingCount>>> Count(DateOnly date, TimeOnly time);
}
