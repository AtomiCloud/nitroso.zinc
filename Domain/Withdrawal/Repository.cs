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
    WithdrawalComplete? complete
  );

  Task<Result<Unit?>> Delete(Guid id);
}
