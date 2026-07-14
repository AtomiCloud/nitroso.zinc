using Domain.Booking;
using FluentAssertions;

namespace UnitTest.Bookings;

// The per-booking analysis costing rule (the SQL CASE mirrors this): the
// ACTUAL KTMB-paid amount when recorded — SGD as-is, MYR × the FX rate
// effective at the booking's CompletedAt — else the per-direction estimate.
// A recorded MYR amount with no effective rate falls back to the estimate
// rather than guessing a conversion.
public class KtmbActualCostTests
{
  private const decimal Estimate = 12m;

  [Fact]
  public void No_actual_amount_falls_back_to_the_estimate()
  {
    KtmbActualCost.Effective(null, null, 0.32m, Estimate).Should().Be(Estimate);
  }

  [Fact]
  public void Sgd_actual_amount_is_used_as_is()
  {
    KtmbActualCost.Effective(10.5m, KtmbActualCost.Sgd, null, Estimate).Should().Be(10.5m);
  }

  [Fact]
  public void Sgd_ignores_the_fx_rate()
  {
    KtmbActualCost.Effective(10.5m, KtmbActualCost.Sgd, 0.32m, Estimate).Should().Be(10.5m);
  }

  [Fact]
  public void Myr_actual_amount_converts_at_the_effective_rate()
  {
    KtmbActualCost.Effective(35m, KtmbActualCost.Myr, 0.30m, Estimate).Should().Be(10.5m);
  }

  [Fact]
  public void Myr_without_an_effective_rate_falls_back_to_the_estimate()
  {
    // no rate row effective at the booking's CompletedAt: never guess a
    // conversion — cost this booking at the per-direction estimate
    KtmbActualCost.Effective(35m, KtmbActualCost.Myr, null, Estimate).Should().Be(Estimate);
  }

  [Fact]
  public void Fallback_estimate_may_be_zero_when_never_configured()
  {
    KtmbActualCost.Effective(null, null, null, 0m).Should().Be(0m);
    KtmbActualCost.Effective(35m, KtmbActualCost.Myr, null, 0m).Should().Be(0m);
  }

  [Fact]
  public void An_unknown_currency_falls_back_to_the_estimate()
  {
    // the validators only admit MYR/SGD; anything else that slips through
    // must degrade to the estimate, not silently miscost
    KtmbActualCost.Effective(35m, "USD", 0.30m, Estimate).Should().Be(Estimate);
  }
}
