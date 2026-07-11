using CSharp_Result;
using Domain.Exceptions;

namespace Domain.Withdrawal;

// Pure fragment planner for card-refund withdrawals: walks the refundable
// pool oldest-payment-first and carves the net amount into per-payment
// fragments. Deterministic — the same pool and net always produce the same
// plan, which the approve flow relies on for idempotent request ids.
public static class RefundPlanner
{
  public static Result<List<(RefundablePayment Payment, decimal Amount)>> Plan(
    decimal net,
    IReadOnlyList<RefundablePayment> pool
  )
  {
    var plan = new List<(RefundablePayment, decimal)>();
    var remaining = net;
    foreach (var payment in pool.OrderBy(x => x.CreatedAt))
    {
      if (remaining <= 0)
        break;
      if (payment.Refundable <= 0)
        continue;
      var take = Math.Min(remaining, payment.Refundable);
      plan.Add((payment, take));
      remaining -= take;
    }

    if (remaining > 0)
      return new InsufficientRefundablePoolException(
        $"The refundable pool (SGD {net - remaining:0.00}) does not cover the net withdrawal amount (SGD {net:0.00})",
        net,
        net - remaining
      );
    return plan;
  }
}
