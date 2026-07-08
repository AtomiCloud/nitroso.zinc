using CSharp_Result;

namespace Domain.Withdrawal;

public interface IWithdrawalService
{
  Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search);

  Task<Result<Withdrawal?>> Get(Guid id, string? userId);

  Task<Result<WithdrawalPrincipal>> Create(string userId, WithdrawalRecord record);

  // User initiated
  Task<Result<WithdrawalPrincipal>> Cancel(Guid id, string userId, string note);

  // Admin initiated
  Task<Result<WithdrawalPrincipal>> Reject(Guid id, string completerId, string note);

  Task<Result<WithdrawalPrincipal>> Complete(
    Guid id,
    string completerId,
    string note,
    Stream receipt
  );

  // Admin or automation initiated: creates a payout at the gateway and moves
  // the withdrawal to Processing; the gateway webhook completes or fails it
  Task<Result<WithdrawalPrincipal>> Approve(Guid id);

  // Gateway webhook: payout settled, collect the reserve and finalize.
  // attempt (parsed from the gateway request id) fences off events from
  // superseded attempts; duplicates of the settling event are acknowledged
  // idempotently, stale events fail with StalePayoutEventException.
  // completerId is null for the webhook; the admin force-complete passes the
  // acting admin for the audit trail.
  Task<Result<WithdrawalPrincipal>> CompletePayout(
    Guid id,
    string confirmationNumber,
    int? attempt,
    string? completerId = null
  );

  // Gateway webhook: payout failed, return the withdrawal to Pending for retry
  Task<Result<WithdrawalPrincipal>> FailPayout(Guid id, string reason, int? attempt);

  // Admin only: finalize a confirmed Processing withdrawal whose settled
  // webhook was permanently lost (verified against the gateway dashboard)
  Task<Result<WithdrawalPrincipal>> ForceCompletePayout(Guid id, string completerId);

  Task<Result<Unit?>> Delete(Guid id);
}
