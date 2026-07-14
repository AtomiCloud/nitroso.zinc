using App.StartUp.Database;
using CSharp_Result;
using Domain.Payment;
using Domain.Withdrawal;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Payments.Data;

public static class GatewayFeeDataMapper
{
  public static GatewayFeeRecord ToRecord(this GatewayFeeData data) =>
    new()
    {
      SourceId = data.SourceId,
      SourceType = (GatewayFeeSourceType)data.SourceType,
      FinancialTransactionId = data.FinancialTransactionId,
      Amount = data.Amount,
      Fee = data.Fee,
      Net = data.Net,
      Currency = data.Currency,
      TransactedAt = data.TransactedAt,
    };

  public static GatewayFeeData UpdateData(this GatewayFeeData data, GatewayFeeRecord record)
  {
    data.SourceId = record.SourceId;
    data.SourceType = (byte)record.SourceType;
    data.FinancialTransactionId = record.FinancialTransactionId;
    data.Amount = record.Amount;
    data.Fee = record.Fee;
    data.Net = record.Net;
    data.Currency = record.Currency;
    data.TransactedAt = record.TransactedAt;
    return data;
  }
}

// Storage for the gateway's own fees. ListPendingSources builds the sync
// worklist (money movements in range with no fee rows yet); Upsert is
// idempotent by FinancialTransactionId via GatewayFeePlanner.
public class GatewayFeeRepository(MainDbContext db, ILogger<GatewayFeeRepository> logger)
  : IGatewayFeeRepository
{
  public async Task<Result<IEnumerable<PendingFeeSource>>> ListPendingSources(
    DateTime after,
    DateTime before,
    IReadOnlyCollection<string> exclude,
    int max
  )
  {
    try
    {
      logger.LogInformation(
        "Listing pending gateway-fee sources in [{After}, {Before}), max {Max}, "
          + "excluding {Excluded}",
        after,
        before,
        max,
        exclude.Count
      );
      var known = db.GatewayFees.Select(f => f.SourceId);
      var excluded = exclude.ToArray();

      // captured intents created in range
      var payments = db
        .Payments.Where(p =>
          p.CapturedAmount > 0
          && p.CreatedAt >= after
          && p.CreatedAt < before
          && !known.Contains(p.ExternalReference)
          && !excluded.Contains(p.ExternalReference)
        )
        .OrderBy(p => p.CreatedAt)
        .Select(p => new PendingFeeSource
        {
          SourceId = p.ExternalReference,
          SourceType = GatewayFeeSourceType.Payment,
        });

      // PayNow payout transfers of withdrawals completed in range
      var completed = (byte)WithdrawStatus.Completed;
      var transfers = db
        .Withdrawals.Where(w =>
          w.Status == completed
          && w.Method == 0
          && w.ConfirmationNumber != null
          && w.CompletedAt != null
          && w.CompletedAt >= after
          && w.CompletedAt < before
          && !known.Contains(w.ConfirmationNumber)
          && !excluded.Contains(w.ConfirmationNumber)
        )
        .OrderBy(w => w.CompletedAt)
        .Select(w => new PendingFeeSource
        {
          SourceId = w.ConfirmationNumber!,
          SourceType = GatewayFeeSourceType.Transfer,
        });

      // card-refund fragments of withdrawals completed in range
      var refunds = db
        .WithdrawalRefunds.Where(r =>
          r.AirwallexRefundId != null
          && r.Withdrawal.Status == completed
          && r.Withdrawal.CompletedAt != null
          && r.Withdrawal.CompletedAt >= after
          && r.Withdrawal.CompletedAt < before
          && !known.Contains(r.AirwallexRefundId)
          && !excluded.Contains(r.AirwallexRefundId)
        )
        .OrderBy(r => r.CreatedAt)
        .Select(r => new PendingFeeSource
        {
          SourceId = r.AirwallexRefundId!,
          SourceType = GatewayFeeSourceType.Refund,
        });

      // three bounded scans (not a union) so each keeps its own index order;
      // the caller passes max = bound + 1, so hitting max signals hasMore
      var result = new List<PendingFeeSource>();
      result.AddRange(await payments.Take(max).ToArrayAsync());
      if (result.Count < max)
        result.AddRange(await transfers.Take(max - result.Count).ToArrayAsync());
      if (result.Count < max)
        result.AddRange(await refunds.Take(max - result.Count).ToArrayAsync());

      return result;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to list pending gateway-fee sources");
      return e;
    }
  }

  public async Task<Result<int>> Upsert(IEnumerable<GatewayFeeRecord> records)
  {
    try
    {
      var incoming = records.ToArray();
      var ids = incoming.Select(x => x.FinancialTransactionId).Distinct().ToArray();
      var existing = await db
        .GatewayFees.Where(x => ids.Contains(x.FinancialTransactionId))
        .ToArrayAsync();

      var (toInsert, toUpdate) = GatewayFeePlanner.Plan(
        existing.Select(x => x.FinancialTransactionId).ToArray(),
        incoming
      );

      var now = DateTime.UtcNow;
      foreach (var record in toInsert)
        db.GatewayFees.Add(new GatewayFeeData { CreatedAt = now }.UpdateData(record));
      foreach (var record in toUpdate)
        existing.First(x => x.FinancialTransactionId == record.FinancialTransactionId)
          .UpdateData(record);

      await db.SaveChangesAsync();
      logger.LogInformation(
        "Upserted gateway fees: {Inserted} inserted, {Updated} refreshed",
        toInsert.Length,
        toUpdate.Length
      );
      return toInsert.Length + toUpdate.Length;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to upsert gateway fees");
      return e;
    }
  }
}
