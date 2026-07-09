using App.Modules.Withdrawals;
using App.StartUp.Options;
using CSharp_Result;
using Domain;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace UnitTest.Withdrawals;

// The withdrawal fee is admin-editable at runtime: the newest stored
// percentage wins, and the configured default only applies while no admin has
// ever set one. A stored 0 must genuinely disable the fee (not fall through
// to the default).
public class FeeCalculatorTests
{
  private static FeeCalculator Make(decimal? stored, decimal configured = 4m) =>
    new(
      new FakeFeeRepository(stored),
      Options.Create(new DomainOptions { WithdrawFeePercentage = configured })
    );

  [Fact]
  public async Task Stored_rate_wins_over_the_configured_default()
  {
    var rate = await Make(stored: 10m).WithdrawFeeRate();
    rate.SuccessOrDefault().Should().Be(0.10m);
  }

  [Fact]
  public async Task Configured_default_applies_while_no_rate_was_ever_set()
  {
    var rate = await Make(stored: null).WithdrawFeeRate();
    rate.SuccessOrDefault().Should().Be(0.04m);
  }

  [Fact]
  public async Task Stored_zero_disables_the_fee_instead_of_falling_back()
  {
    var rate = await Make(stored: 0m).WithdrawFeeRate();
    rate.SuccessOrDefault().Should().Be(0m);

    var fee = await Make(stored: 0m).WithdrawFee(123.45m);
    fee.SuccessOrDefault().Should().Be(0m);
  }

  [Fact]
  public async Task Fee_is_rounded_to_even_cents()
  {
    // 2.5% of $1.00 = $0.025 → banker's rounding gives $0.02
    var fee = await Make(stored: 2.5m).WithdrawFee(1.00m);
    fee.SuccessOrDefault().Should().Be(0.02m);
  }

  private sealed class FakeFeeRepository(decimal? stored) : IFeeRepository
  {
    public Task<Result<decimal?>> GetLatestPercentage() =>
      Task.FromResult<Result<decimal?>>(stored);

    public Task<Result<decimal>> SetPercentage(decimal percentage) =>
      Task.FromResult<Result<decimal>>(percentage);
  }
}
