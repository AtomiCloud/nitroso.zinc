namespace Domain;

public interface IFeeCalculator
{
  decimal WithdrawFeeRate { get; }

  decimal WithdrawFee(decimal amount);
}
