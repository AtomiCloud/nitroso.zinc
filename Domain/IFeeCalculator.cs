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

// Admin mutation surface for the fee rate
public interface IFeeRepository
{
  // newest rate, or null when no admin has ever set one
  Task<Result<decimal?>> GetLatestPercentage();

  Task<Result<decimal>> SetPercentage(decimal percentage);
}
