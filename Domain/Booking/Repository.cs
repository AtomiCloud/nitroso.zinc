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

  // breakdown = the price composition captured at purchase; null for flows
  // that have no quote to persist (older callers, duplicated bookings)
  Task<Result<BookingPrincipal>> Create(
    string userId,
    Guid transactionId,
    BookingRecord record,
    BookingPriceBreakdown? breakdown
  );

  Task<Result<BookingPrincipal?>> Update(
    string? userId,
    Guid id,
    BookingStatus? status,
    BookingRecord? record,
    BookingComplete? complete
  );

  Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly Time);

  // marks the booking priority (front of its queue), snapshots the fee
  // charged (null = FREE boost, nothing charged so nothing to refund), stamps
  // PrioritizedAt and — when someone other than the owner (an admin) invoked
  // it — the granter's sub for boost-ledger attribution; null result when the
  // booking does not exist (or is not visible to userId)
  Task<Result<BookingPrincipal?>> Prioritize(
    string? userId,
    Guid id,
    decimal? fee,
    string? grantedBy
  );

  // bumps the recovery retry counter by one; null when the booking does not
  // exist. Runs inside the caller's RecoverRevert transaction so the counter
  // and the status transition always move together.
  Task<Result<BookingPrincipal?>> IncrementRecoveryRetries(Guid id);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  Task<Result<IEnumerable<BookingCount>>> Count(
    DateOnly date,
    TimeOnly time,
    DateOnly? filterDate,
    TrainDirection? filterDirection
  );
}
