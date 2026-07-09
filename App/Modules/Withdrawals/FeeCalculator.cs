using App.StartUp.Options;
using CSharp_Result;
using Domain;
using Microsoft.Extensions.Options;

namespace App.Modules.Withdrawals;

public class FeeCalculator(IFeeRepository repo, IOptions<DomainOptions> d) : IFeeCalculator
{
  private static decimal Dd => 100;

  // the newest admin-set rate wins; the configured default applies while no
  // admin has ever set one
  public Task<Result<decimal>> WithdrawFeeRate()
  {
    return repo.GetLatestPercentage()
      .Then(p => (p ?? d.Value.WithdrawFeePercentage) / Dd, Errors.MapNone);
  }

  // Rounded to cents so the ledger, the payout and the user-visible numbers
  // always agree
  public Task<Result<decimal>> WithdrawFee(decimal amount)
  {
    return this.WithdrawFeeRate()
      .Then(rate => Math.Round(amount * rate, 2, MidpointRounding.ToEven), Errors.MapNone);
  }
}
