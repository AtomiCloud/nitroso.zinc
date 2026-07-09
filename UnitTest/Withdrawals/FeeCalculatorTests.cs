using App.Modules.Withdrawals;
using CSharp_Result;
using Domain;
using FluentAssertions;

namespace UnitTest.Withdrawals;

// Fees are a per-type (Withdrawal | Deposit) queue of flat + percentage
// change events. The newest effective event wins; with NO event the fee is
// zero-zero — fees only exist once an admin queues one. The computed fee is
// flat + pct x amount, rounded to even cents and capped at the amount.
public class FeeCalculatorTests
{
  private static FeeCalculator Make(
    FeeType type = FeeType.Withdrawal,
    decimal? percentage = null,
    decimal? flat = null
  ) =>
    new(
      new FakeFeeRepository(
        percentage == null && flat == null
          ? null
          : new FeeChange
          {
            Id = Guid.NewGuid(),
            Type = type,
            Percentage = percentage ?? 0m,
            FlatAmount = flat ?? 0m,
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
      DateTime? effectiveAt
    ) =>
      Task.FromResult<Result<FeeChange>>(
        new FeeChange
        {
          Id = Guid.NewGuid(),
          Type = type,
          Percentage = percentage,
          FlatAmount = flatAmount,
          EffectiveAt = effectiveAt ?? DateTime.UtcNow,
        }
      );

    public Task<Result<FeeChange?>> CancelUpcoming(Guid id) =>
      Task.FromResult<Result<FeeChange?>>((FeeChange?)null);
  }
}
