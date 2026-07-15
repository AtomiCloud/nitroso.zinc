using System.Linq.Expressions;
using Domain.Booking;

namespace App.Modules.Bookings.Data;

// The tin backfill worklist predicates in one place (the BookingQueue
// convention): bookings that captured a KTMB reservation (both identifiers
// present) but have no actual paid amount (or no captured termination
// refund) recorded yet. ListMissingKtmbCost/ListMissingKtmbRefund page with
// them DB-side and the unit tests pin them in-memory — they must always
// agree.
public static class KtmbBackfill
{
  // the actual-cost worklist for one status: Completed for the original
  // completion backfill, Terminated for terminated-then-refunded history
  // (a termination still bought a ticket first, so its cost is a fact too)
  public static Expression<Func<BookingData, bool>> MissingFor(BookStatus status) =>
    x =>
      x.Status == (byte)status
      && x.CompletedAt != null
      && x.BookingNo != null
      && x.TicketNo != null
      && x.KtmbAmount == null;

  public static readonly Expression<Func<BookingData, bool>> Missing = MissingFor(
    BookStatus.Completed
  );

  // the refund worklist: Terminated bookings whose actual KTMB cost is
  // recorded (without it the refund is uninterpretable for P&L) but whose
  // KTMB termination refund has not been captured yet
  public static readonly Expression<Func<BookingData, bool>> RefundMissing = x =>
    x.Status == (byte)BookStatus.Terminated
    && x.CompletedAt != null
    && x.BookingNo != null
    && x.TicketNo != null
    && x.KtmbAmount != null
    && x.KtmbRefundAmount == null;
}
