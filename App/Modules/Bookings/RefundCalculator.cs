using App.StartUp.Options;
using Domain;
using Microsoft.Extensions.Options;

namespace App.Modules.Bookings;

public class RefundCalculator(IOptions<DomainOptions> d) : IRefundCalculator
{
  private decimal Nn => d.Value.RefundPercentage;
  private static decimal Dd => 100;

  public decimal RefundRate => this.Nn / Dd;

  public decimal PenaltyRate => 1 - this.RefundRate;
}
