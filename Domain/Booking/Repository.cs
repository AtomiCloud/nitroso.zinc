using CSharp_Result;
using Domain.Timings;

namespace Domain.Booking;

public interface IBookingRepository
{
  Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search);

  // total matches for the same filters, ignoring Limit/Skip — lets the UI
  // paginate with real page numbers
  Task<Result<int>> SearchCount(BookingSearch search);

  // null when the booking does not exist (or is not visible to userId)
  Task<Result<BookingQueuePosition?>> QueuePosition(string? userId, Guid id);

  Task<Result<IEnumerable<BookingStatRow>>> Stats(BookingStatsQuery query);

  Task<Result<IEnumerable<BookingPrincipal>>> RefundList(DateOnly date, TimeOnly time);

  Task<Result<Booking?>> Get(string? userId, Guid id);

  Task<Result<BookingPrincipal>> Create(string userId, Guid transactionId, BookingRecord record);

  Task<Result<BookingPrincipal?>> Update(
    string? userId,
    Guid id,
    BookingStatus? status,
    BookingRecord? record,
    BookingComplete? complete
  );

  Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly Time);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  Task<Result<IEnumerable<BookingCount>>> Count(
    DateOnly date,
    TimeOnly time,
    DateOnly? filterDate,
    TrainDirection? filterDirection
  );
}
