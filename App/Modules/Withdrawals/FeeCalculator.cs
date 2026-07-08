using App.StartUp.Options;
using Domain;
using Microsoft.Extensions.Options;

namespace App.Modules.Withdrawals;

public class FeeCalculator(IOptions<DomainOptions> d) : IFeeCalculator
{
  private decimal Nn => d.Value.WithdrawFeePercentage;
  private static decimal Dd => 100;

  public decimal WithdrawFeeRate => this.Nn / Dd;

  // Rounded to cents so the ledger, the payout and the user-visible numbers
  // always agree
  public decimal WithdrawFee(decimal amount) =>
    Math.Round(amount * this.WithdrawFeeRate, 2, MidpointRounding.ToEven);
}
