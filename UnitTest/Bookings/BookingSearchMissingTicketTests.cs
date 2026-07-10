using App.Modules.Bookings.API.V1;
using FluentAssertions;

namespace UnitTest.Bookings;

// The MissingTicket search filter is the ticket-repair worklist: Completed
// bookings whose Ticket file reference is NULL. The repo applies it DB-level;
// here we pin the API contract — the flag threads into the domain query and
// contradictory Status filters are rejected upfront.
public class BookingSearchMissingTicketTests
{
  private readonly BookingSearchQueryValidator validator = new();

  private static SearchBookingQuery Query(bool? missingTicket, string? status = null) =>
    new(
      Date: null,
      Direction: null,
      Status: status,
      Time: null,
      UserId: null,
      PassportNumber: null,
      PassengerName: null,
      StuckForMinutes: null,
      MissingTicket: missingTicket,
      SortBy: null,
      Limit: null,
      Skip: null
    );

  [Theory]
  [InlineData(null)]
  [InlineData(true)]
  [InlineData(false)]
  public void MissingTicket_threads_into_the_domain_search(bool? missingTicket)
  {
    Query(missingTicket).ToDomain().MissingTicket.Should().Be(missingTicket);
  }

  [Fact]
  public void MissingTicket_alone_is_valid()
  {
    validator.Validate(Query(true)).IsValid.Should().BeTrue();
  }

  [Fact]
  public void MissingTicket_with_completed_status_is_valid()
  {
    validator.Validate(Query(true, "Completed")).IsValid.Should().BeTrue();
  }

  [Theory]
  [InlineData("Pending")]
  [InlineData("Buying")]
  [InlineData("Recovering")]
  [InlineData("Refunded")]
  public void MissingTicket_with_non_completed_status_is_rejected(string status)
  {
    // MissingTicket implies Completed; any other status filter can only
    // produce an empty page — reject the contradiction instead
    var result = validator.Validate(Query(true, status));
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.PropertyName == "Status");
  }

  [Fact]
  public void Non_completed_status_without_missing_ticket_stays_valid()
  {
    validator.Validate(Query(null, "Pending")).IsValid.Should().BeTrue();
    validator.Validate(Query(false, "Pending")).IsValid.Should().BeTrue();
  }
}
