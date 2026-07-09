using System.Linq.Expressions;

namespace App.Modules.Bookings.Data;

// The purchase-queue ordering in one place: priority first, then oldest
// CreatedAt, Id as the deterministic tiebreak. Reserve() sorts by these keys
// and QueuePosition() counts with AheadOf — they must always agree.
public static class BookingQueue
{
  // EF-translatable (b's fields are captured constants): x is ahead of b iff
  // x outranks b on priority, or ties on priority and booked earlier
  public static Expression<Func<BookingData, bool>> AheadOf(BookingData b) =>
    x =>
      (x.Priority && !b.Priority)
      || (
        x.Priority == b.Priority
        && (x.CreatedAt < b.CreatedAt || (x.CreatedAt == b.CreatedAt && x.Id < b.Id))
      );
}
