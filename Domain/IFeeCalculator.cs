using CSharp_Result;

namespace Domain;

// The live withdrawal fee. Async because the rate is admin-editable at
// runtime (stored as insert-only rows; the newest wins, with the configured
// default as fallback while no row exists).
public interface IFeeCalculator
{
  Task<Result<decimal>> WithdrawFeeRate();

  Task<Result<decimal>> WithdrawFee(decimal amount);
}

// A fee rate change: the percentage and the instant it takes (or took) effect
public record FeeChange
{
  public required decimal Percentage { get; init; }

  public required DateTime EffectiveAt { get; init; }
}

// Admin mutation surface for the fee rate
public interface IFeeRepository
{
  // the rate currently in effect (newest row whose EffectiveAt has passed),
  // or null when no admin has ever set one
  Task<Result<decimal?>> GetLatestPercentage();

  // rate changes scheduled in the future, soonest first
  Task<Result<IEnumerable<FeeChange>>> GetUpcoming();

  // effectiveAt null = immediate
  Task<Result<FeeChange>> SetPercentage(decimal percentage, DateTime? effectiveAt);
}
