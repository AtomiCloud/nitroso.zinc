using CSharp_Result;

namespace Domain.Withdrawal;

// Persistence port for the card-refund rail: the funding payments a wallet
// may be refunded against, and the refund fragments (evidence rows) created
// against them
public interface IWithdrawalRefundRepository
{
  // Captured gateway card payments of the wallet created at or after `since`,
  // oldest first — the raw refundable pool before subtracting prior refunds
  Task<Result<List<FundingPayment>>> ListFundingPayments(Guid walletId, DateTime since);

  // Sum of non-Failed fragment amounts per payment id, across ALL
  // withdrawals (absent key = nothing refunded against that payment yet)
  Task<Result<Dictionary<Guid, decimal>>> SumActiveRefundsByPayment(IEnumerable<Guid> paymentIds);

  // All fragments of a withdrawal (every attempt), oldest first
  Task<Result<List<WithdrawalRefundFragment>>> ListByWithdrawal(Guid withdrawalId);

  Task<Result<WithdrawalRefundFragment?>> GetByRequestId(string requestId);

  Task<Result<List<WithdrawalRefundFragment>>> CreateMany(
    IEnumerable<WithdrawalRefundFragment> fragments
  );

  // Partial update: null leaves the field untouched (SettledAt is only
  // written together with a Settled status)
  Task<Result<WithdrawalRefundFragment?>> Update(
    Guid id,
    RefundFragmentStatus? status,
    string? airwallexRefundId,
    DateTime? settledAt
  );
}
