using System.Linq.Expressions;

namespace App.Modules.Bookings.Data;

// The purchase-queue ordering in one place: priority first — earliest boost
// (PrioritizedAt) wins within the priority group, falling back to CreatedAt
// for boosts that predate the audit column — then oldest CreatedAt, Id as the
// deterministic tiebreak. Reserve() sorts by these keys and QueuePosition()
// counts with AheadOf — they must always agree.
public static class BookingQueue
{
  // EF-translatable (b's fields are captured constants): x is ahead of b iff
  // x outranks b on priority, or ties on priority and boosted earlier
  // (booking age, then Id, break boost-time ties)
  public static Expression<Func<BookingData, bool>> AheadOf(BookingData b)
  {
    var bKey = b.PrioritizedAt ?? b.CreatedAt;
    return x =>
      (x.Priority && !b.Priority)
      || (
        x.Priority == b.Priority
        && (
          (x.PrioritizedAt ?? x.CreatedAt) < bKey
          || (
            (x.PrioritizedAt ?? x.CreatedAt) == bKey
            && (x.CreatedAt < b.CreatedAt || (x.CreatedAt == b.CreatedAt && x.Id < b.Id))
          )
        )
      );
  }
}
