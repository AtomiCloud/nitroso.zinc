using App.Modules.Bookings.Data;
using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// The tin backfill worklist predicates (KtmbBackfill): bookings that
// captured a KTMB reservation (BookingNo + TicketNo present) but have no
// actual paid amount (Missing/MissingFor) or no captured termination refund
// (RefundMissing) recorded yet. ListMissingKtmbCost/ListMissingKtmbRefund
// page with them DB-side — these tests pin the filters in-memory.
public class KtmbBackfillWorklistTests
{
  private static readonly Func<BookingData, bool> Missing = KtmbBackfill.Missing.Compile();

  private static readonly Func<BookingData, bool> MissingTerminated = KtmbBackfill
    .MissingFor(BookStatus.Terminated)
    .Compile();

  private static readonly Func<BookingData, bool> RefundMissing =
    KtmbBackfill.RefundMissing.Compile();

  private static BookingData Booking(
    BookStatus status = BookStatus.Completed,
    string? bookingNo = "BN-1",
    string? ticketNo = "TN-1",
    decimal? ktmbAmount = null,
    DateTime? completedAt = null,
    decimal? ktmbRefundAmount = null
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      Status = (byte)status,
      CompletedAt = completedAt ?? new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
      BookingNo = bookingNo,
      TicketNo = ticketNo,
      KtmbAmount = ktmbAmount,
      KtmbCurrency = ktmbAmount == null ? null : "MYR",
      KtmbRefundAmount = ktmbRefundAmount,
      KtmbRefundCurrency = ktmbRefundAmount == null ? null : "MYR",
    };

  [Fact]
  public void A_completed_booking_with_identifiers_and_no_actual_cost_is_on_the_worklist()
  {
    Missing(Booking()).Should().BeTrue();
  }

  [Fact]
  public void A_booking_with_a_recorded_actual_cost_is_off_the_worklist()
  {
    Missing(Booking(ktmbAmount: 35.5m)).Should().BeFalse();
  }

  [Theory]
  [InlineData(BookStatus.Pending)]
  [InlineData(BookStatus.Buying)]
  [InlineData(BookStatus.Recovering)]
  [InlineData(BookStatus.Cancelled)]
  [InlineData(BookStatus.Refunded)]
  [InlineData(BookStatus.Terminated)]
  [InlineData(BookStatus.Duplicate)]
  [InlineData(BookStatus.RequireManualIntervention)]
  public void A_non_completed_booking_is_off_the_worklist(BookStatus status)
  {
    Missing(Booking(status)).Should().BeFalse();
  }

  [Fact]
  public void A_booking_without_a_booking_number_is_off_the_worklist()
  {
    // no KTMB identifiers = nothing tin can look up on KTMB's side
    Missing(Booking(bookingNo: null)).Should().BeFalse();
  }

  [Fact]
  public void A_booking_without_a_ticket_number_is_off_the_worklist()
  {
    Missing(Booking(ticketNo: null)).Should().BeFalse();
  }

  [Fact]
  public void A_booking_without_a_completion_instant_is_off_the_worklist()
  {
    var b = Booking();
    b.CompletedAt = null;
    Missing(b).Should().BeFalse();
  }

  // ---- MissingFor(Terminated): the terminated-history cost worklist ----

  [Fact]
  public void A_terminated_booking_with_identifiers_and_no_actual_cost_is_on_the_terminated_worklist()
  {
    MissingTerminated(Booking(BookStatus.Terminated)).Should().BeTrue();
  }

  [Fact]
  public void A_completed_booking_is_off_the_terminated_worklist()
  {
    // the status-parameterized worklists never overlap — each status pages
    // its own backfill
    MissingTerminated(Booking()).Should().BeFalse();
  }

  [Fact]
  public void A_terminated_booking_with_a_recorded_actual_cost_is_off_the_terminated_worklist()
  {
    MissingTerminated(Booking(BookStatus.Terminated, ktmbAmount: 35.5m)).Should().BeFalse();
  }

  // ---- RefundMissing: the termination-refund worklist ----

  [Fact]
  public void A_terminated_booking_with_a_cost_but_no_refund_is_on_the_refund_worklist()
  {
    RefundMissing(Booking(BookStatus.Terminated, ktmbAmount: 35.5m)).Should().BeTrue();
  }

  [Fact]
  public void A_terminated_booking_with_a_captured_refund_is_off_the_refund_worklist()
  {
    RefundMissing(Booking(BookStatus.Terminated, ktmbAmount: 35.5m, ktmbRefundAmount: 20m))
      .Should()
      .BeFalse();
  }

  [Fact]
  public void A_terminated_booking_without_a_recorded_cost_is_off_the_refund_worklist()
  {
    // without the actual cost the refund is uninterpretable for P&L — the
    // cost backfill (ktmb-cost/missing?status=5) must run first
    RefundMissing(Booking(BookStatus.Terminated)).Should().BeFalse();
  }

  [Theory]
  [InlineData(BookStatus.Pending)]
  [InlineData(BookStatus.Buying)]
  [InlineData(BookStatus.Completed)]
  [InlineData(BookStatus.Recovering)]
  [InlineData(BookStatus.Cancelled)]
  [InlineData(BookStatus.Refunded)]
  [InlineData(BookStatus.Duplicate)]
  [InlineData(BookStatus.RequireManualIntervention)]
  public void A_non_terminated_booking_is_off_the_refund_worklist(BookStatus status)
  {
    RefundMissing(Booking(status, ktmbAmount: 35.5m)).Should().BeFalse();
  }

  [Fact]
  public void A_terminated_booking_without_identifiers_is_off_the_refund_worklist()
  {
    RefundMissing(Booking(BookStatus.Terminated, bookingNo: null, ktmbAmount: 35.5m))
      .Should()
      .BeFalse();
    RefundMissing(Booking(BookStatus.Terminated, ticketNo: null, ktmbAmount: 35.5m))
      .Should()
      .BeFalse();
  }

  [Fact]
  public void A_terminated_booking_without_a_completion_instant_is_off_the_refund_worklist()
  {
    var b = Booking(BookStatus.Terminated, ktmbAmount: 35.5m);
    b.CompletedAt = null;
    RefundMissing(b).Should().BeFalse();
  }
}
