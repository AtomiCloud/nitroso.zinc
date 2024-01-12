using CSharp_Result;

namespace Domain.Withdrawal;

public interface IWithdrawalStorage
{
  Task<Result<string>> Save(Stream stream);

  Task<Result<string>> Get(string key);
}
