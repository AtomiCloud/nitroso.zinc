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

// A refund as the GATEWAY knows it, listed by creation window rather than
// looked up by id. This is the only handle on refunds issued manually before
// the CardRefund method existed: zinc stored no refund id for those, so
// GetRefundStatus cannot reach them and the reconciliation has to comb.
public record GatewayRefund
{
  public required string Id { get; init; }

  // the payment intent the money returned to — the link back to a zinc
  // payment, and from there to the owning wallet and user
  public required string PaymentIntentId { get; init; }

  public required decimal Amount { get; init; }

  public required PayoutOutcome Outcome { get; init; }

  public required string? AcquirerReferenceNumber { get; init; }

  // null when the gateway did not report one; a candidate withdrawal then
  // cannot be scored on time proximity and stays ambiguous rather than being
  // guessed at
  public required DateTime? CreatedAt { get; init; }

  // Last state change at the gateway. For a settled refund this is the nearest
  // available settlement time — the gateway publishes no settled_at — so it is
  // what an attached fragment records as SettledAt.
  public required DateTime? UpdatedAt { get; init; }

  // The merchant-supplied idempotency key. zinc-issued refunds carry the
  // "{withdrawalId}-{attempt}-{index}" fragment RequestId here, which names
  // the owning withdrawal outright — a manually-issued refund does not, which
  // is exactly why the rest of the matching exists.
  public required string? RequestId { get; init; }
}

public interface IRefundGateway
{
  Task<Result<RefundConfirmation>> CreateRefund(RefundRequest request);

  // Point-in-time gateway view of a refund, used by the reconciliation sweep
  // and the ARN backfill (refunds are only addressable by their gateway id)
  Task<Result<RefundStatus>> GetRefundStatus(string refundId);

  // Every refund the gateway created in [fromUtc, toUtc). Used by the
  // historic-refund reconciliation, which has no refund ids to look up.
  // Bounded by the gateway's 2-year retention: an older window answers empty,
  // and those refunds can never be recovered.
  Task<Result<List<GatewayRefund>>> ListRefunds(DateTime fromUtc, DateTime toUtc);
}
