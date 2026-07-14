using App.Modules.Bookings.API.V1;
using FluentAssertions;

namespace UnitTest.Bookings;

// The ticket-cost estimate on GET Booking/analysis is opt-in now that actual
// KTMB costs are backfilled: an absent (or false) Estimate binds to a query
// that costs bookings without an actual at 0, while Estimate=true keeps the
// legacy KtmbCosts-fallback behavior byte-for-byte — existing callers that
// never sent the parameter keep a valid request (After/Before still bind).
public class BookingAnalysisEstimateQueryTests
{
  [Fact]
  public void Absent_estimate_defaults_to_no_estimate_fallback()
  {
    var req = new BookingAnalysisQueryReq("01-08-2026", "31-08-2026", null);

    var domain = req.ToDomain();

    domain.Estimate.Should().BeFalse();
    domain.After.Should().Be(new DateOnly(2026, 8, 1));
    domain.Before.Should().Be(new DateOnly(2026, 8, 31));
  }

  [Fact]
  public void Estimate_true_opts_back_into_the_legacy_fallback()
  {
    var req = new BookingAnalysisQueryReq(null, null, true);

    var domain = req.ToDomain();

    domain.Estimate.Should().BeTrue();
    domain.After.Should().BeNull();
    domain.Before.Should().BeNull();
  }

  [Fact]
  public void Estimate_false_behaves_like_absent()
  {
    var req = new BookingAnalysisQueryReq(null, null, false);

    req.ToDomain().Estimate.Should().BeFalse();
  }
}
