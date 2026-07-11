using CSharp_Result;

namespace Domain.Withdrawal;

public record RefundRequest
{
  // Unique per fragment and attempt ("{withdrawalId}-{attempt}-{index}"); the
  // gateway deduplicates on this, so a re-sent fragment can never create a
  // second refund
  public required string RequestId { get; init; }

  // the gateway payment intent the money returns to
  public required string PaymentIntentId { get; init; }

  public required decimal Amount { get; init; }
}

// Confirmation of a created refund; Id is the gateway's refund identifier
public record RefundConfirmation
{
  public required string Id { get; init; }
}

public interface IRefundGateway
{
  Task<Result<RefundConfirmation>> CreateRefund(RefundRequest request);

  // Point-in-time gateway view of a refund, used by the reconciliation sweep
  // (refunds are only addressable by their gateway id)
  Task<Result<PayoutStatus>> GetRefundStatus(string refundId);
}
