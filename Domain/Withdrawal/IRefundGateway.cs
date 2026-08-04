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

// Point-in-time gateway view of a refund. Deliberately NOT PayoutStatus,
// which this lookup used to borrow: the ARN is a card-network concept that a
// PayNow transfer can never have, and bolting it onto the shared payout type
// would hand every payout call site a field that is null by construction.
// Outcome still reuses PayoutOutcome — the in-flight/settled/failed/not-found
// classification is genuinely the same question for both rails.
public record RefundStatus
{
  public required PayoutOutcome Outcome { get; init; }

  // the gateway refund id; present whenever the gateway knows the refund
  public required string? ConfirmationNumber { get; init; }

  // Acquirer Reference Number, the card network's handle on the refund and
  // the identifier the tax export carries. Published only once the refund
  // settles, so null here means "not yet", not "never".
  public required string? AcquirerReferenceNumber { get; init; }
}

public interface IRefundGateway
{
  Task<Result<RefundConfirmation>> CreateRefund(RefundRequest request);

  // Point-in-time gateway view of a refund, used by the reconciliation sweep
  // and the ARN backfill (refunds are only addressable by their gateway id)
  Task<Result<RefundStatus>> GetRefundStatus(string refundId);
}
