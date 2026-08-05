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

  // Settled fragments that hold a gateway refund id but no ARN yet, oldest
  // first, bounded to those created at or after `createdOnOrAfter` — the
  // gateway stops answering for refunds beyond its retention window, so rows
  // older than that are excluded rather than retried forever. `excludeIds`
  // drops fragments this run already asked about and got no ARN for, so one
  // drain never re-reads the same rows batch after batch.
  Task<Result<List<WithdrawalRefundFragment>>> ListSettledMissingArn(
    DateTime createdOnOrAfter,
    IEnumerable<Guid> excludeIds,
    int max
  );

  // Fragments already holding any of these gateway refund ids. The historic
  // reconciliation's idempotency gate: a refund zinc already has evidence for
  // must never gain a second fragment, however the first one got there.
  Task<Result<List<WithdrawalRefundFragment>>> ListByAirwallexRefundIds(
    IEnumerable<string> refundIds
  );

  // Who the given gateway payment intents belong to (absent = zinc has no
  // payment row for that intent, so no withdrawal of ours can own its refund)
  Task<Result<List<PaymentIntentOwner>>> ListPaymentIntentOwners(
    IEnumerable<string> paymentIntentIds
  );

  // Withdrawals of these wallets that could own a historic refund, with their
  // already-attached refund totals. Scoped to the wallets the refunds' intents
  // resolved to, so the candidate set stays small however wide the window is.
  Task<Result<List<WithdrawalCandidate>>> ListCandidatesByWallets(IEnumerable<Guid> walletIds);

  // How many settled fragments still lack an ARN but sit beyond the gateway's
  // retention window — permanently unbackfillable, and reported so the gap in
  // the tax export is visible instead of silent
  Task<Result<int>> CountUnbackfillableArn(DateTime createdBefore);

  // Partial update: null leaves the field untouched (SettledAt is only
  // written together with a Settled status; a null ARN must never erase a
  // previously-captured one)
  Task<Result<WithdrawalRefundFragment?>> Update(
    Guid id,
    RefundFragmentStatus? status,
    string? airwallexRefundId,
    DateTime? settledAt,
    string? acquirerReferenceNumber
  );
}
