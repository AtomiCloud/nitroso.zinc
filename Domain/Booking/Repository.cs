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

  // priority bookings currently queued (Pending/Buying/Recovering) in the
  // timeslot — the numerator SlotCap is checked against
  Task<Result<int>> CountSlotPriority(TrainDirection direction, DateOnly date, TimeOnly time);

  // marks the booking priority (into the queue's priority group, which is
  // ordered by boost time — PrioritizedAt), snapshots the fee
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

  // the tin backfill worklist: bookings of the given status (Completed, or
  // Terminated for terminated-then-refunded history) that captured a KTMB
  // reservation (BookingNo + TicketNo present) but have no actual paid
  // amount recorded yet, oldest CompletedAt first (Id as the stable tiebreak)
  Task<Result<IEnumerable<BookingKtmbCostMissing>>> ListMissingKtmbCost(
    BookStatus status,
    int limit,
    int skip
  );

  // the tin refund-backfill worklist: Terminated bookings with a recorded
  // actual KTMB cost but no captured KTMB refund yet, oldest CompletedAt
  // first (Id as the stable tiebreak)
  Task<Result<IEnumerable<BookingKtmbCostMissing>>> ListMissingKtmbRefund(int limit, int skip);

  // bumps the recovery retry counter by one; null when the booking does not
  // exist. Runs inside the caller's RecoverRevert transaction so the counter
  // and the status transition always move together.
  Task<Result<BookingPrincipal?>> IncrementRecoveryRetries(Guid id);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  // PDPA account wipe: the ticket blob keys referenced by this user's
  // bookings (tickets are PDFs carrying passenger name/passport) — collected
  // before the scrub so the objects can be removed from blob storage
  Task<Result<string[]>> ListTicketKeys(string userId);

  // PDPA account wipe: scrubs passenger-identifying content from ALL of this
  // user's bookings — blanks the embedded passenger snapshot (FullName,
  // PassportNumber, PassportExpiry, Gender) and nulls the Ticket blob
  // reference — while keeping the rows themselves (BookingNo, TicketNo,
  // amounts, KTMB costs/refunds are revenue records). Returns the number of
  // bookings scrubbed.
  Task<Result<int>> WipePersonalData(string userId);

  Task<Result<IEnumerable<BookingCount>>> Count(
    DateOnly date,
    TimeOnly time,
    DateOnly? filterDate,
    TrainDirection? filterDirection
  );
}
