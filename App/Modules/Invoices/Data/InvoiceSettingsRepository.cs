using App.StartUp.Database;
using CSharp_Result;
using Domain.Invoice;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Invoices.Data;

public static class InvoiceSettingsDataMapper
{
  public static InvoiceSettingsChange ToChange(this InvoiceSettingsData data) =>
    new()
    {
      Id = data.Id,
      MarketingSharePct = data.MarketingSharePct,
      Infrastructure = data.Infrastructure,
      RecoveryPerBoost = data.RecoveryPerBoost,
      RecoveryPerTicket = data.RecoveryPerTicket,
      EffectiveAt = data.EffectiveAt,
      CreatedAt = data.CreatedAt,
    };

  public static InvoicePartnerChange ToChange(this InvoicePartnerData data) =>
    new()
    {
      Id = data.Id,
      Suffix = data.Suffix,
      Name = data.Name,
      RoundingPreference = (InvoiceRoundingPreference)data.RoundingPreference,
      Active = data.Active,
      Position = data.Position,
      EffectiveAt = data.EffectiveAt,
      CreatedAt = data.CreatedAt,
    };
}

// The invoice's agreed terms. Insert-only: both Add methods append a row and
// nothing ever updates or deletes one, so the queue is also the audit trail.
public class InvoiceSettingsRepository(MainDbContext db, ILogger<InvoiceSettingsRepository> logger)
  : IInvoiceSettingsRepository
{
  // Normalize to UTC exactly like KtmbCostRepository.Add: JSON without a Z
  // binds as Unspecified and an offset binds as Local — Npgsql rejects both
  // for timestamptz.
  private static DateTime Normalize(DateTime? effectiveAt, DateTime now) =>
    effectiveAt?.Kind switch
    {
      null => now,
      DateTimeKind.Utc => effectiveAt.Value,
      DateTimeKind.Local => effectiveAt.Value.ToUniversalTime(),
      _ => DateTime.SpecifyKind(effectiveAt.Value, DateTimeKind.Utc),
    };

  public async Task<Result<IEnumerable<InvoiceSettingsChange>>> ListSettings()
  {
    try
    {
      var rows = await db.InvoiceSettings.OrderBy(x => x.EffectiveAt).ToArrayAsync();
      return rows.Select(x => x.ToChange()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to list invoice settings changes");
      return e;
    }
  }

  public async Task<Result<IEnumerable<InvoicePartnerChange>>> ListPartners()
  {
    try
    {
      var rows = await db.InvoicePartners.OrderBy(x => x.EffectiveAt).ToArrayAsync();
      return rows.Select(x => x.ToChange()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to list invoice partner changes");
      return e;
    }
  }

  public async Task<Result<InvoiceSettingsChange>> AddSettings(
    InvoiceSettingsChange change,
    DateTime? effectiveAt
  )
  {
    try
    {
      var now = DateTime.UtcNow;
      var effective = Normalize(effectiveAt, now);
      logger.LogInformation(
        "Queueing invoice settings change: share {Share}%, infra {Infra}, "
          + "recovery {PerBoost}/boost {PerTicket}/ticket, effective {EffectiveAt}",
        change.MarketingSharePct,
        change.Infrastructure,
        change.RecoveryPerBoost,
        change.RecoveryPerTicket,
        effective
      );
      var data = new InvoiceSettingsData
      {
        CreatedAt = now,
        EffectiveAt = effective,
        MarketingSharePct = change.MarketingSharePct,
        Infrastructure = change.Infrastructure,
        RecoveryPerBoost = change.RecoveryPerBoost,
        RecoveryPerTicket = change.RecoveryPerTicket,
      };
      db.InvoiceSettings.Add(data);
      await db.SaveChangesAsync();
      return data.ToChange();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to queue invoice settings change");
      return e;
    }
  }

  public async Task<Result<InvoicePartnerChange>> AddPartner(
    InvoicePartnerChange change,
    DateTime? effectiveAt
  )
  {
    try
    {
      var now = DateTime.UtcNow;
      var effective = Normalize(effectiveAt, now);
      logger.LogInformation(
        "Queueing invoice partner change: {Suffix} ({Name}), active {Active}, "
          + "effective {EffectiveAt}",
        change.Suffix,
        change.Name,
        change.Active,
        effective
      );
      var data = new InvoicePartnerData
      {
        CreatedAt = now,
        EffectiveAt = effective,
        Suffix = change.Suffix,
        Name = change.Name,
        RoundingPreference = (byte)change.RoundingPreference,
        Active = change.Active,
        Position = change.Position,
      };
      db.InvoicePartners.Add(data);
      await db.SaveChangesAsync();
      return data.ToChange();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to queue invoice partner change");
      return e;
    }
  }
}
