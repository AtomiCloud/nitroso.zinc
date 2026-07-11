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

  public async Task<Result<WithdrawalRefundFragment?>> Update(
    Guid id,
    RefundFragmentStatus? status,
    string? airwallexRefundId,
    DateTime? settledAt
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
