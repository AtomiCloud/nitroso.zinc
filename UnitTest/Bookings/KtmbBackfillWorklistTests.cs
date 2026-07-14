using App.Modules.Bookings.Data;
using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// The tin backfill worklist predicate (KtmbBackfill.Missing): Completed
// bookings that captured a KTMB reservation (BookingNo + TicketNo present)
// but have no actual paid amount recorded yet. ListMissingKtmbCost pages
// with it DB-side — these tests pin the filter in-memory.
public class KtmbBackfillWorklistTests
{
  private static readonly Func<BookingData, bool> Missing = KtmbBackfill.Missing.Compile();

  private static BookingData Booking(
    BookStatus status = BookStatus.Completed,
    string? bookingNo = "BN-1",
    string? ticketNo = "TN-1",
    decimal? ktmbAmount = null,
    DateTime? completedAt = null
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
}
