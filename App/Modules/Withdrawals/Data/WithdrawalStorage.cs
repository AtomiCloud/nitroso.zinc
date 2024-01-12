using App.Modules.Common;
using App.StartUp.Registry;
using CSharp_Result;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.Data;

public class WithdrawalStorage(IFileRepository file) : IWithdrawalStorage
{
  public Task<Result<string>> Save(Stream stream)
  {
    return file.Save(BlockStorages.Main, "withdrawal", Guid.NewGuid().ToString(), stream, true);
  }

  public Task<Result<string>> Get(string key)
  {
    return file.SignedLink(BlockStorages.Main, key, 60 * 60);
  }
}
