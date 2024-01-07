namespace Domain;

public interface IRefundCalculator
{
  decimal RefundRate { get; }

  decimal PenaltyRate { get; }
}
