using CSharp_Result;

namespace Domain.Withdrawal;

public record PayoutRequest
{
  // Unique per attempt; the gateway deduplicates on this, so a retried attempt
  // can never create a second payout for the same request id
  public required string RequestId { get; init; }

  // Net amount actually paid out (gross minus withdrawal fee)
  public required decimal Amount { get; init; }

  public required string PayNowNumber { get; init; }
}

// Confirmation of a created payout; Id is the gateway's transfer identifier and
// doubles as the withdrawal's confirmation number
public record PayoutConfirmation
{
  public required string Id { get; init; }
}

public enum PayoutOutcome
{
  // the gateway has the transfer but it is neither settled nor dead yet
  InFlight = 0,
  Settled = 1,
  Failed = 2,

  // the gateway has no transfer for this request id: the create call never
  // landed (safe to retry via re-drive)
  NotFound = 3,
}

// Point-in-time gateway view of a payout, used by the reconciliation sweep
public record PayoutStatus
{
  public required PayoutOutcome Outcome { get; init; }

  // present whenever the gateway knows the transfer
  public required string? ConfirmationNumber { get; init; }
}

public interface IPayoutGateway
{
  Task<Result<PayoutConfirmation>> CreatePayout(PayoutRequest request);

  // Looks the payout up at the gateway — by transfer id when confirmed,
  // else by the deterministic request id
  Task<Result<PayoutStatus>> GetPayoutStatus(string requestId, string? confirmationNumber);
}
