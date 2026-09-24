using App.StartUp.Database;
using CSharp_Result;
using Domain.Payment;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Payments.Data;

public static class KtmbTopupDataMapper
{
  public static KtmbTopupRecord ToRecord(this KtmbTopupData data) =>
    new()
    {
      IssuingTransactionId = data.IssuingTransactionId,
      PostedAt = data.PostedAt,
      AmountMyr = data.AmountMyr,
      AmountSgd = data.AmountSgd,
      MerchantName = data.MerchantName,
      Source = (KtmbTopupSource)data.Source,
    };

  public static KtmbTopupData UpdateData(this KtmbTopupData data, KtmbTopupRecord record)
  {
    data.IssuingTransactionId = record.IssuingTransactionId;
    data.PostedAt = record.PostedAt;
    data.AmountMyr = record.AmountMyr;
    data.AmountSgd = record.AmountSgd;
    data.MerchantName = record.MerchantName;
    data.Source = (byte)record.Source;
    return data;
  }
}

// Storage for the KTMB card top-ups. Upsert is idempotent by
// IssuingTransactionId via KtmbTopupPlanner; LatestGatewayPostedAt is the
// sweep's watermark and reads only swept rows.
public class KtmbTopupRepository(MainDbContext db, ILogger<KtmbTopupRepository> logger)
  : IKtmbTopupRepository
{
  public async Task<Result<DateTime?>> LatestGatewayPostedAt()
  {
    try
    {
      var gateway = (byte)KtmbTopupSource.Gateway;
      var latest = await db
        .KtmbTopups.Where(x => x.Source == gateway)
        .MaxAsync(x => (DateTime?)x.PostedAt);
      return latest;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to read the KTMB top-up sweep watermark");
      return e;
    }
  }

  public async Task<Result<int>> Upsert(IEnumerable<KtmbTopupRecord> records)
  {
    try
    {
      var incoming = records.ToArray();
      if (incoming.Length == 0)
        return 0;

      var ids = incoming.Select(x => x.IssuingTransactionId).Distinct().ToArray();
      var existing = await db
        .KtmbTopups.Where(x => ids.Contains(x.IssuingTransactionId))
        .ToArrayAsync();

      var (toInsert, toUpdate) = KtmbTopupPlanner.Plan(
        existing.Select(x => x.IssuingTransactionId).ToArray(),
        incoming
      );

      var now = DateTime.UtcNow;
      foreach (var record in toInsert)
        db.KtmbTopups.Add(new KtmbTopupData { CreatedAt = now }.UpdateData(record));

      // A refresh never rewrites Source: a row an admin entered by hand stays
      // Manual even if the sweep later finds the same transaction, so the
      // watermark cannot be dragged around by back-entered history.
      foreach (var record in toUpdate)
      {
        var row = existing.First(x => x.IssuingTransactionId == record.IssuingTransactionId);
        var source = row.Source;
        row.UpdateData(record);
        row.Source = source;
      }

      await db.SaveChangesAsync();
      logger.LogInformation(
        "Upserted KTMB top-ups: {Inserted} inserted, {Updated} refreshed",
        toInsert.Length,
        toUpdate.Length
      );
      return toInsert.Length + toUpdate.Length;
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to upsert KTMB top-ups");
      return e;
    }
  }
}
