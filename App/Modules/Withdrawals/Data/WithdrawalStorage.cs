using App.Modules.Common;
using App.StartUp.Registry;
using CSharp_Result;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.Data;

public class WithdrawalStorage(IFileRepository file) : IWithdrawalStorage
{
  private static readonly TimeSpan MinimumSignedLinkExpiry = TimeSpan.FromSeconds(1);
  private static readonly TimeSpan MaximumSignedLinkExpiry = TimeSpan.FromDays(7);

  public Task<Result<string>> Save(Stream stream)
  {
    return file.Save(BlockStorages.Main, "withdrawal", Guid.NewGuid().ToString(), stream, true);
  }

  public Task<Result<string>> Get(string key)
  {
    return this.Get(key, TimeSpan.FromHours(1));
  }

  public Task<Result<string>> Get(string key, TimeSpan expiry)
  {
    if (expiry < MinimumSignedLinkExpiry || expiry > MaximumSignedLinkExpiry)
    {
      return Task.FromResult<Result<string>>(
        new ArgumentOutOfRangeException(
          nameof(expiry),
          expiry,
          "Receipt link expiry must be between one second and seven days."
        )
      );
    }

    return file.SignedLink(BlockStorages.Main, key, (int)expiry.TotalSeconds);
  }
}
