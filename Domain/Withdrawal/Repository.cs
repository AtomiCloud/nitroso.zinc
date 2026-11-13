using CSharp_Result;

namespace Domain.Withdrawal;

public interface IWithdrawalRepository
{
  Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search);

  Task<Result<Withdrawal?>> Get(Guid id, string? userId);

  Task<Result<WithdrawalPrincipal>> Create(Guid walletId, WithdrawalRecord record);

  Task<Result<WithdrawalPrincipal?>> Update(
    string? userId,
    Guid id,
    WithdrawalRecord? record,
    WithdrawalStatus? status,
    WithdrawalComplete? complete,
    WithdrawalPayout? payout = null
  );

  Task<Result<Unit?>> Delete(Guid id);

  // Atomically clear the reconcile strike counter, only while the withdrawal
  // is still Processing on payout attempt `attempt` with strikes to clear.
  // One conditional UPDATE of that single column — never a whole-row write —
  // so it can never put a withdrawal a concurrent webhook just completed or
  // failed back into Processing. Returns the rows changed (0 = state moved on
  // or nothing to clear).
  Task<Result<int>> ClearReconcileAttempts(Guid id, int attempt);
}
