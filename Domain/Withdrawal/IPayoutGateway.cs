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

public interface IPayoutGateway
{
  Task<Result<PayoutConfirmation>> CreatePayout(PayoutRequest request);
}
