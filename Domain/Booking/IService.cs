using CSharp_Result;
using Domain.Timings;

namespace Domain.Booking;

public interface IBookingService
{
  Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search);

  Task<Result<int>> SearchCount(BookingSearch search);

  Task<Result<BookingQueuePosition?>> QueuePosition(string? userId, Guid id);

  Task<Result<IEnumerable<BookingStatRow>>> Stats(BookingStatsQuery query);

  Task<Result<IEnumerable<BookingPrincipal>>> ListRefunds(DateTime referenceTime);

  Task<Result<Booking?>> Get(string? userId, Guid id);

  // breakdown = the price composition behind cost, persisted for component
  // analytics (null when the caller has no materialized quote)
  Task<Result<BookingPrincipal>> Create(
    string userId,
    decimal cost,
    BookingRecord record,
    BookingPriceBreakdown? breakdown = null
  );

  Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingRecord record);

  Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly time);

  Task<Result<BookingPrincipal?>> Buying(Guid id);

  Task<Result<BookingPrincipal?>> Recovering(Guid id);

  Task<Result<BookingPrincipal?>> Duplicate(Guid id);

  Task<Result<BookingPrincipal?>> ManualIntervention(Guid id);

  Task<Result<BookingPrincipal?>> Revert(Guid id, bool force);

  // recycle a 'Recovering' booking back to 'Pending' for another purchase
  // attempt, counting the attempt; refuses with
  // RecoveryRetriesExhaustedException once maxRetries recycles happened
  Task<Result<BookingPrincipal?>> RecoverRevert(Guid id, int maxRetries);

  // repair the ticket artefacts of an already-Completed booking: replace the
  // ticket file reference and backfill missing identifiers — no money moves,
  // no status change
  Task<Result<BookingPrincipal?>> AttachTicket(
    Guid id,
    string? bookingNo,
    string? ticketNo,
    Stream file
  );

  // cheap single-booking probe: does the booking carry a ticket reference,
  // and does that reference resolve to a real stored object
  Task<Result<BookingTicketHealth?>> TicketHealth(Guid id);

  Task<Result<BookingPrincipal?>> Complete(Guid id,
    string bookingNo,
    string ticketNo,
    Stream ticketFile);

  Task<Result<BookingPrincipal?>> CompleteNoCollect(Guid id,
    string bookingNo,
    string ticketNo,
    Stream ticketFile);

  Task<Result<BookingPrincipal?>> Cancel(string? userId, Guid id);

  Task<Result<BookingPrincipal>> Terminate(string? userId, Guid id, DateTime referenceTime);

  Task<Result<BookingPrincipal>> Refund(Guid id);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  Task<Result<IEnumerable<BookingCount>>> Count();

  Task<Result<IEnumerable<BookingCount>>> Count(BookingCountSearch query);

  // may this user prioritize a booking right now, at what fee, and is the
  // boost free for them; callerTokenRoles = the target user's own JWT roles
  // when they are the caller (null = fall back to the persisted Roles mirror).
  // slot = the timeslot about to be purchased (no booking exists yet): hour-
  // bounded policies apply against its departure and its slot cap is counted.
  // No slot: hour-bounded policies are skipped, SlotsLeft null. Either way a
  // percent fee cannot be computed without a charged ticket (Fee = null).
  Task<Result<PriorityEligibility>> PriorityEligibility(
    string userId,
    string[]? callerTokenRoles = null,
    (TrainDirection Direction, DateOnly Date, TimeOnly Time)? slot = null
  );

  // booking-scoped flavor: hour-bounded policies apply (hours to the
  // timeslot's departure) and the per-timeslot slot cap is counted; null
  // when the booking does not exist (or is not visible to userId)
  Task<Result<PriorityEligibility?>> BookingPriorityEligibility(
    string? userId,
    Guid id,
    string[]? callerTokenRoles = null
  );

  // charge the priority fee and move the booking into its queue's priority
  // group (ahead of every non-priority booking; boost time orders the group);
  // callerSub = the invoking user's sub, persisted as the granter when it is
  // not the booking owner (admin-granted boost attribution)
  Task<Result<BookingPrincipal?>> Prioritize(string? userId, Guid id, string? callerSub = null);
}
