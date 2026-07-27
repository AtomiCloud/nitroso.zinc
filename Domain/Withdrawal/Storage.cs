using CSharp_Result;

namespace Domain.Withdrawal;

public interface IWithdrawalStorage
{
  Task<Result<string>> Save(Stream stream);

  Task<Result<string>> Get(string key);

  // A presigned link with an explicit lifetime, for the tax export. S3/MinIO
  // cap presigned GET expiry at 7 days, so this cannot make an archival CSV's
  // receipt links durable on its own — the export's trailing `receipt_key`
  // column is the permanent, re-signable reference; this link is a
  // best-effort convenience.
  Task<Result<string>> Get(string key, TimeSpan expiry);
}
