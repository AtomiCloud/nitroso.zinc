using CSharp_Result;

namespace Domain.Withdrawal;

public record WithdrawalExportCursor(DateTime CreatedAt, Guid Id);

// Read-only projection for the tax-evidence CSV. Unlike the normal list
// repository method, each page carries every relation required to render a
// self-describing ledger row.
public interface IWithdrawalExportRepository
{
  Task<Result<IReadOnlyList<Withdrawal>>> SearchExport(
    WithdrawalSearch search,
    int limit,
    WithdrawalExportCursor? cursor = null,
    CancellationToken cancellationToken = default
  );
}
