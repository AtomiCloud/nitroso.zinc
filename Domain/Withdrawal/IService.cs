using CSharp_Result;

namespace Domain.Withdrawal;

public interface IWithdrawalService
{
  // How far back captured card payments still count toward the refundable
  // pool, unless configured otherwise (Withdrawal:RefundWindowDays)
  const int DefaultRefundWindowDays = 180;

  Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search);

  Task<Result<Withdrawal?>> Get(Guid id, string? userId);

  // refundWindowDays: how far back captured card payments still count toward
  // the refundable pool (config Withdrawal:RefundWindowDays); a CardRefund
  // creation is refused when the pool cannot cover the net amount
  Task<Result<WithdrawalPrincipal>> Create(
    string userId,
    WithdrawalRecord record,
    int refundWindowDays = DefaultRefundWindowDays
  );

  // The user's refundable pool: captured gateway card payments inside the
  // window, minus refunds already issued against them (any withdrawal)
  Task<Result<decimal>> RefundablePool(
    string userId,
    int refundWindowDays = DefaultRefundWindowDays
  );

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

  // Admin or automation initiated: moves the withdrawal to Processing and
  // creates the payout at the gateway — a PayNow transfer, or (CardRefund)
  // one refund per funding payment, planned oldest-first from the refundable
  // pool. The gateway webhook completes or fails it. A CardRefund whose pool
  // no longer covers the net amount is returned to Pending with a
  // distinguishable error before any refund is created.
  Task<Result<WithdrawalPrincipal>> Approve(
    Guid id,
    int refundWindowDays = DefaultRefundWindowDays
  );

  // Gateway webhook: a refund fragment settled. Records the evidence
  // idempotently, and when ALL fragments of the current attempt are settled,
  // collects the reserve and completes the withdrawal (identical ledger to
  // the PayNow rail).
  Task<Result<WithdrawalPrincipal>> SettleRefundFragment(
    Guid id,
    string requestId,
    string refundId,
    int? attempt
  );

  // Gateway webhook: a refund fragment terminally failed. Sibling fragments
  // may already be settled — money has partially left — so the withdrawal is
  // parked RequireManualIntervention with the evidence visible, never
  // returned to Pending.
  Task<Result<WithdrawalPrincipal>> FailRefundFragment(
    Guid id,
    string requestId,
    string refundId,
    string reason,
    int? attempt
  );

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

  // Admin only: finalize a confirmed Processing/parked withdrawal whose
  // settled webhook was permanently lost (verified against the gateway)
  Task<Result<WithdrawalPrincipal>> ForceCompletePayout(Guid id, string completerId);

  // Reconciliation sweep (admin or tin): look the transfer up at the gateway;
  // settle/fail accordingly, else count the sweep and park at the cap
  Task<Result<WithdrawalPrincipal>> Reconcile(Guid id);

  // Admin only: RequireManualIntervention -> Pending, after verifying at the
  // gateway that no live transfer exists
  Task<Result<WithdrawalPrincipal>> Requeue(Guid id);

  Task<Result<Unit?>> Delete(Guid id);

  // The withdrawal settings in effect right now: the newest settings row, or
  // the domain defaults when none was ever written
  Task<Result<WithdrawalSettingsRecord>> GetCurrentSettings();

  // Replace the withdrawal settings (insert-only latest, like PrioritySettings)
  Task<Result<WithdrawalSettingsPrincipal>> CreateSettings(WithdrawalSettingsRecord record);
}
