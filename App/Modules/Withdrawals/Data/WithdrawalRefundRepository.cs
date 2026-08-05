using App.StartUp.Database;
using CSharp_Result;
using Domain.Withdrawal;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Withdrawals.Data;

public class WithdrawalRefundRepository(
  MainDbContext db,
  ILogger<WithdrawalRefundRepository> logger
) : IWithdrawalRefundRepository
{
  private const string Gateway = "Airwallex";

  // a captured Airwallex payment intent: the only status under which money
  // actually arrived and may be refunded
  private const string CapturedStatus = "SUCCEEDED";

  public async Task<Result<List<FundingPayment>>> ListFundingPayments(
    Guid walletId,
    DateTime since
  )
  {
    try
    {
      logger.LogInformation(
        "Listing funding payments for Wallet '{WalletId}' since {Since}",
        walletId,
        since
      );
      var payments = await db
        .Payments.Where(x =>
          x.WalletId == walletId
          && x.Gateway == Gateway
          && x.Status == CapturedStatus
          && x.CreatedAt >= since
          && x.CapturedAmount > 0
        )
        .OrderBy(x => x.CreatedAt)
        .Select(x => new
        {
          x.Id,
          x.ExternalReference,
          x.CreatedAt,
          x.CapturedAmount,
        })
        .ToArrayAsync();
      return payments
        .Select(x => new FundingPayment
        {
          PaymentId = x.Id,
          PaymentIntentId = x.ExternalReference,
          CreatedAt = x.CreatedAt,
          CapturedAmount = x.CapturedAmount,
        })
        .ToList();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed listing funding payments for Wallet '{WalletId}'", walletId);
      throw;
    }
  }

  public async Task<Result<Dictionary<Guid, decimal>>> SumActiveRefundsByPayment(
    IEnumerable<Guid> paymentIds
  )
  {
    try
    {
      var ids = paymentIds.ToArray();
      var sums = await db
        .WithdrawalRefunds.Where(x =>
          ids.Contains(x.PaymentId) && x.Status != (byte)RefundFragmentStatus.Failed
        )
        .GroupBy(x => x.PaymentId)
        .Select(g => new { PaymentId = g.Key, Total = g.Sum(x => x.Amount) })
        .ToArrayAsync();
      return sums.ToDictionary(x => x.PaymentId, x => x.Total);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed summing active refunds by payment");
      throw;
    }
  }

  public async Task<Result<List<WithdrawalRefundFragment>>> ListByWithdrawal(Guid withdrawalId)
  {
    try
    {
      var rows = await db
        .WithdrawalRefunds.Where(x => x.WithdrawalId == withdrawalId)
        .OrderBy(x => x.RequestId)
        .ToArrayAsync();
      return rows.Select(x => x.ToDomain()).ToList();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed listing refund fragments for Withdrawal '{Id}'", withdrawalId);
      throw;
    }
  }

  public async Task<Result<WithdrawalRefundFragment?>> GetByRequestId(string requestId)
  {
    try
    {
      var row = await db
        .WithdrawalRefunds.Where(x => x.RequestId == requestId)
        .FirstOrDefaultAsync();
      return row?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed getting refund fragment by request id '{RequestId}'", requestId);
      throw;
    }
  }

  public async Task<Result<List<WithdrawalRefundFragment>>> CreateMany(
    IEnumerable<WithdrawalRefundFragment> fragments
  )
  {
    try
    {
      var rows = fragments.Select(x => x.ToData()).ToList();
      logger.LogInformation(
        "Creating {Count} refund fragments for Withdrawal '{WithdrawalId}'",
        rows.Count,
        rows.FirstOrDefault()?.WithdrawalId
      );
      db.WithdrawalRefunds.AddRange(rows);
      await db.SaveChangesAsync();
      return rows.Select(x => x.ToDomain()).ToList();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed creating refund fragments");
      return e;
    }
  }

  public async Task<Result<List<WithdrawalRefundFragment>>> ListByAirwallexRefundIds(
    IEnumerable<string> refundIds
  )
  {
    try
    {
      var ids = refundIds.Distinct(StringComparer.Ordinal).ToArray();
      if (ids.Length == 0)
        return new List<WithdrawalRefundFragment>();
      var rows = await db
        .WithdrawalRefunds.Where(x =>
          x.AirwallexRefundId != null && ids.Contains(x.AirwallexRefundId)
        )
        .ToArrayAsync();
      return rows.Select(x => x.ToDomain()).ToList();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed listing refund fragments by gateway refund id");
      return e;
    }
  }

  public async Task<Result<List<PaymentIntentOwner>>> ListPaymentIntentOwners(
    IEnumerable<string> paymentIntentIds
  )
  {
    try
    {
      var ids = paymentIntentIds.Distinct(StringComparer.Ordinal).ToArray();
      if (ids.Length == 0)
        return new List<PaymentIntentOwner>();
      var rows = await db
        .Payments.Where(x => x.Gateway == Gateway && ids.Contains(x.ExternalReference))
        .Select(x => new
        {
          x.Id,
          x.ExternalReference,
          x.WalletId,
          x.Wallet.UserId,
        })
        .ToArrayAsync();
      return rows
        .Select(x => new PaymentIntentOwner
        {
          PaymentId = x.Id,
          PaymentIntentId = x.ExternalReference,
          WalletId = x.WalletId,
          UserId = x.UserId,
        })
        .ToList();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed resolving payment intent owners");
      return e;
    }
  }

  public async Task<Result<List<WithdrawalCandidate>>> ListCandidatesByWallets(
    IEnumerable<Guid> walletIds
  )
  {
    try
    {
      var ids = walletIds.Distinct().ToArray();
      if (ids.Length == 0)
        return new List<WithdrawalCandidate>();
      var rows = await db
        .Withdrawals.Where(x => ids.Contains(x.WalletId))
        .Select(x => new
        {
          x.Id,
          x.WalletId,
          x.Wallet.UserId,
          x.Method,
          x.Status,
          x.Amount,
          x.Fee,
          x.CreatedAt,
          x.CompletedAt,
          // non-Failed only, matching SumActiveRefundsByPayment: a failed
          // fragment released its claim, so it explains none of the amount
          Attached = x
            .Refunds.Where(r => r.Status != (byte)RefundFragmentStatus.Failed)
            .Sum(r => (decimal?)r.Amount),
        })
        .ToArrayAsync();
      return rows
        .Select(x => new WithdrawalCandidate
        {
          Id = x.Id,
          WalletId = x.WalletId,
          UserId = x.UserId,
          Method = (WithdrawalMethod)x.Method,
          Status = (WithdrawStatus)x.Status,
          Amount = x.Amount,
          Fee = x.Fee,
          CreatedAt = x.CreatedAt,
          CompletedAt = x.CompletedAt,
          AttachedRefundTotal = x.Attached ?? 0m,
        })
        .ToList();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed listing withdrawal candidates by wallet");
      return e;
    }
  }

  public async Task<Result<List<WithdrawalRefundFragment>>> ListSettledMissingArn(
    DateTime createdOnOrAfter,
    IEnumerable<Guid> excludeIds,
    int max
  )
  {
    try
    {
      var excluded = excludeIds.ToArray();
      var settled = (byte)RefundFragmentStatus.Settled;
      var rows = await db
        .WithdrawalRefunds.Where(x =>
          x.Status == settled
          && x.AirwallexRefundId != null
          && x.AcquirerReferenceNumber == null
          && x.CreatedAt >= createdOnOrAfter
          && !excluded.Contains(x.Id)
        )
        // oldest first: those are closest to ageing out of the gateway's
        // retention window, so they are the ones worth spending calls on
        .OrderBy(x => x.CreatedAt)
        .Take(max)
        .ToArrayAsync();
      return rows.Select(x => x.ToDomain()).ToList();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed listing settled refund fragments missing an ARN");
      return e;
    }
  }

  public async Task<Result<int>> CountUnbackfillableArn(DateTime createdBefore)
  {
    try
    {
      var settled = (byte)RefundFragmentStatus.Settled;
      return await db.WithdrawalRefunds.CountAsync(x =>
        x.Status == settled
        && x.AirwallexRefundId != null
        && x.AcquirerReferenceNumber == null
        && x.CreatedAt < createdBefore
      );
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed counting unbackfillable refund fragments");
      return e;
    }
  }

  public async Task<Result<WithdrawalRefundFragment?>> Update(
    Guid id,
    RefundFragmentStatus? status,
    string? airwallexRefundId,
    DateTime? settledAt,
    string? acquirerReferenceNumber
  )
  {
    try
    {
      logger.LogInformation(
        "Updating refund fragment '{Id}' with status {Status}, refund id '{RefundId}'",
        id,
        status,
        airwallexRefundId
      );
      var row = await db.WithdrawalRefunds.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (row == null)
        return (WithdrawalRefundFragment?)null;

      if (status != null)
        row.Status = (byte)status;
      if (airwallexRefundId != null)
        row.AirwallexRefundId = airwallexRefundId;
      if (settledAt != null)
        row.SettledAt = settledAt;
      if (acquirerReferenceNumber != null)
        row.AcquirerReferenceNumber = acquirerReferenceNumber;

      var updated = db.WithdrawalRefunds.Update(row);
      await db.SaveChangesAsync();
      return updated.Entity.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed updating refund fragment '{Id}'", id);
      return e;
    }
  }
}
