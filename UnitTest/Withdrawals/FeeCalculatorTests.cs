using App.Modules.Withdrawals;
using CSharp_Result;
using Domain;
using FluentAssertions;

namespace UnitTest.Withdrawals;

// Fees are a per-type (Withdrawal | Deposit | Termination) queue of flat +
// percentage change events. The newest effective event wins; with NO event
// the fee is zero-zero — fees only exist once an admin queues one. The
// computed fee is flat + pct x amount, rounded to even cents and capped at
// min(amount, Cap) — the fee can exceed neither what is moved nor the
// admin-set ceiling.
public class FeeCalculatorTests
{
  private static FeeCalculator Make(
    FeeType type = FeeType.Withdrawal,
    decimal? percentage = null,
    decimal? flat = null,
    decimal? cap = null
  ) =>
    new(
      new FakeFeeRepository(
        percentage == null && flat == null && cap == null
          ? null
          : new FeeChange
          {
            Id = Guid.NewGuid(),
            Type = type,
            Percentage = percentage ?? 0m,
            FlatAmount = flat ?? 0m,
            Cap = cap,
            EffectiveAt = DateTime.UtcNow.AddDays(-1),
          }
      )
    );

  [Fact]
  public async Task No_stored_event_means_zero_zero()
  {
    var spec = await Make().Current(FeeType.Withdrawal);
    spec.SuccessOrDefault().Should().Be(FeeSpec.None);

    var fee = await Make().Compute(FeeType.Withdrawal, 100m);
    fee.SuccessOrDefault().Should().Be(0m);
  }

  [Fact]
  public async Task Percentage_only_charges_pct_of_amount()
  {
    var fee = await Make(percentage: 4m).Compute(FeeType.Withdrawal, 100m);
    fee.SuccessOrDefault().Should().Be(4.00m);
  }

  [Fact]
  public async Task Flat_only_charges_the_flat_amount()
  {
    var fee = await Make(flat: 1.50m).Compute(FeeType.Withdrawal, 100m);
    fee.SuccessOrDefault().Should().Be(1.50m);
  }

  [Fact]
  public async Task Flat_and_percentage_combine()
  {
    // 4% of $100 = $4.00, plus $1.50 flat = $5.50
    var fee = await Make(percentage: 4m, flat: 1.50m).Compute(FeeType.Withdrawal, 100m);
    fee.SuccessOrDefault().Should().Be(5.50m);
  }

  [Fact]
  public async Task Stored_zero_zero_disables_the_fee()
  {
    var fee = await Make(percentage: 0m, flat: 0m).Compute(FeeType.Withdrawal, 123.45m);
    fee.SuccessOrDefault().Should().Be(0m);
  }

  [Fact]
  public async Task Fee_is_rounded_to_even_cents()
  {
    // 2.5% of $1.00 = $0.025 → banker's rounding gives $0.02
    var fee = await Make(percentage: 2.5m).Compute(FeeType.Withdrawal, 1.00m);
    fee.SuccessOrDefault().Should().Be(0.02m);
  }

  [Fact]
  public async Task Fee_is_capped_at_the_amount()
  {
    // a $5 flat fee on a $3 withdrawal must never exceed the $3 being moved
    var fee = await Make(flat: 5m).Compute(FeeType.Withdrawal, 3m);
    fee.SuccessOrDefault().Should().Be(3m);
  }

  [Fact]
  public async Task Deposit_fee_uses_the_deposit_type()
  {
    var calc = Make(type: FeeType.Deposit, percentage: 2m);
    var fee = await calc.Compute(FeeType.Deposit, 50m);
    fee.SuccessOrDefault().Should().Be(1.00m);
  }

  // ---- Cap ----

  [Theory]
  [InlineData(FeeType.Withdrawal)]
  [InlineData(FeeType.Deposit)]
  [InlineData(FeeType.Termination)]
  public async Task Cap_binds_when_the_computed_fee_exceeds_it_for_every_type(FeeType type)
  {
    // 50% of $100 = $50, capped at $10
    var fee = await Make(type: type, percentage: 50m, cap: 10m).Compute(type, 100m);
    fee.SuccessOrDefault().Should().Be(10m, "the fee is min(computed, cap)");
  }

  [Theory]
  [InlineData(FeeType.Withdrawal)]
  [InlineData(FeeType.Deposit)]
  [InlineData(FeeType.Termination)]
  public async Task Cap_above_the_computed_fee_changes_nothing(FeeType type)
  {
    // 4% of $100 = $4.00 — a $10 cap does not bind
    var fee = await Make(type: type, percentage: 4m, cap: 10m).Compute(type, 100m);
    fee.SuccessOrDefault().Should().Be(4.00m);
  }

  [Fact]
  public async Task Flat_plus_percentage_plus_cap_combine()
  {
    // $1.50 flat + 4% of $100 = $5.50, capped at $5
    var capped = await Make(percentage: 4m, flat: 1.50m, cap: 5m)
      .Compute(FeeType.Withdrawal, 100m);
    capped.SuccessOrDefault().Should().Be(5m);

    // same spec with a roomy cap: the raw $5.50 survives
    var uncapped = await Make(percentage: 4m, flat: 1.50m, cap: 6m)
      .Compute(FeeType.Withdrawal, 100m);
    uncapped.SuccessOrDefault().Should().Be(5.50m);
  }

  [Fact]
  public async Task Amount_still_caps_even_when_cap_is_larger()
  {
    // $5 flat on a $3 move with a $4 cap: min(amount, cap) = $3 wins
    var fee = await Make(flat: 5m, cap: 4m).Compute(FeeType.Withdrawal, 3m);
    fee.SuccessOrDefault().Should().Be(3m);

    // and the cap wins when it is the smaller of the two
    var fee2 = await Make(flat: 5m, cap: 2m).Compute(FeeType.Withdrawal, 3m);
    fee2.SuccessOrDefault().Should().Be(2m);
  }

  [Fact]
  public async Task Null_cap_leaves_existing_behavior_unchanged()
  {
    var fee = await Make(percentage: 4m, flat: 1.50m).Compute(FeeType.Withdrawal, 100m);
    fee.SuccessOrDefault().Should().Be(5.50m, "no cap = the pre-cap math verbatim");
  }

  [Fact]
  public async Task Degenerate_amounts_stay_free_with_a_cap()
  {
    var fee = await Make(percentage: 50m, cap: 10m).Compute(FeeType.Termination, 0m);
    fee.SuccessOrDefault().Should().Be(0m);
  }

  // ---- Termination (live-parity seed: 50%, no flat, no cap) ----

  [Fact]
  public async Task Termination_at_fifty_percent_halves_the_amount()
  {
    var fee = await Make(type: FeeType.Termination, percentage: 50m)
      .Compute(FeeType.Termination, 16m);
    fee.SuccessOrDefault().Should().Be(8m, "50% seed keeps parity with Amount * RefundRate");
  }

  private sealed class FakeFeeRepository(FeeChange? current) : IFeeRepository
  {
    public Task<Result<FeeChange?>> GetCurrent(FeeType type) =>
      Task.FromResult<Result<FeeChange?>>(current?.Type == type ? current : null);

    public Task<Result<IEnumerable<FeeChange>>> GetUpcoming(FeeType type) =>
      Task.FromResult<Result<IEnumerable<FeeChange>>>(Array.Empty<FeeChange>());

    public Task<Result<FeeChange>> Add(
      FeeType type,
      decimal percentage,
      decimal flatAmount,
      decimal? cap,
      DateTime? effectiveAt
    ) =>
      Task.FromResult<Result<FeeChange>>(
        new FeeChange
        {
          Id = Guid.NewGuid(),
          Type = type,
          Percentage = percentage,
          FlatAmount = flatAmount,
          Cap = cap,
          EffectiveAt = effectiveAt ?? DateTime.UtcNow,
        }
      );

    public Task<Result<FeeChange?>> CancelUpcoming(Guid id) =>
      Task.FromResult<Result<FeeChange?>>((FeeChange?)null);
  }
}
