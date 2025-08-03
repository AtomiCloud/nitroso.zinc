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

  Task<Result<Unit?>> Delete(Guid id);
}
