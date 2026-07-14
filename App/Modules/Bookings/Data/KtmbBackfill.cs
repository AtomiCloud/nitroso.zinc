using System.Linq.Expressions;
using Domain.Booking;

namespace App.Modules.Bookings.Data;

// The tin backfill worklist predicate in one place (the BookingQueue
// convention): Completed bookings that captured a KTMB reservation (both
// identifiers present) but have no actual paid amount recorded yet.
// ListMissingKtmbCost pages with it DB-side and the unit tests pin it
// in-memory — they must always agree.
public static class KtmbBackfill
{
  public static readonly Expression<Func<BookingData, bool>> Missing = x =>
    x.Status == (byte)BookStatus.Completed
    && x.CompletedAt != null
    && x.BookingNo != null
    && x.TicketNo != null
    && x.KtmbAmount == null;
}
