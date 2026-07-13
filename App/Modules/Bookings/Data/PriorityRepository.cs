using App.Modules.Discounts.Data;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Booking;
using Domain.Discount;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

public class PrioritySettingsRepository(
  MainDbContext db,
  ILogger<PrioritySettingsRepository> logger
) : IPrioritySettingsRepository
{
  public async Task<Result<PrioritySettingsRecord>> GetCurrent()
  {
    try
    {
      var r = await db
        .PrioritySettings.OrderByDescending(x => x.CreatedAt)
        .ThenByDescending(x => x.Id)
        .FirstOrDefaultAsync();

      // unified row: '[]' is a legitimate "nobody boosts" configuration
      if (r?.Policies != null)
        return new PrioritySettingsRecord
        {
          Policies = r.Policies.Select(p => p.ToRecord()).ToList(),
        };

      // pre-unification row (or none at all): synthesize equivalent rules
      // from the legacy fields + the allowlist so behavior is unchanged
      var allowlist = await db.PriorityAccesses.Select(x => x.UserId).ToArrayAsync();
      return new PrioritySettingsRecord
      {
        Policies = PriorityDataMapper.SynthesizeLegacy(r, allowlist),
      };
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to read the current priority settings");
      return e;
    }
  }

  public async Task<Result<PrioritySettingsPrincipal>> Create(PrioritySettingsRecord record)
  {
    try
    {
      logger.LogInformation("Creating PrioritySettings: {@Record}", record.ToJson());
      var data = new PrioritySettingsData { CreatedAt = DateTime.UtcNow };
      data.UpdateData(record);
      var r = db.PrioritySettings.Add(data);
      await db.SaveChangesAsync();
      return new PrioritySettingsPrincipal
      {
        Id = r.Entity.Id,
        CreatedAt = r.Entity.CreatedAt,
        Record = record,
      };
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create PrioritySettings {@Record}", record.ToJson());
      return e;
    }
  }
}

public class PriorityAccessRepository(MainDbContext db, ILogger<PriorityAccessRepository> logger)
  : IPriorityAccessRepository
{
  public async Task<Result<IEnumerable<PriorityAccess>>> List()
  {
    try
    {
      logger.LogInformation("Listing priority access users");
      var r = await db
        .PriorityAccesses.OrderBy(x => x.CreatedAt)
        .ThenBy(x => x.UserId)
        .ToArrayAsync();
      return r.Select(x => x.ToDomain()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed listing priority access users");
      return e;
    }
  }

  public async Task<Result<PriorityAccess>> Add(string userId)
  {
    try
    {
      logger.LogInformation("Adding priority access for User '{UserId}'", userId);
      var existing = await db
        .PriorityAccesses.Where(x => x.UserId == userId)
        .FirstOrDefaultAsync();
      // idempotent: allowlisting twice is a no-op
      if (existing != null)
        return existing.ToDomain();
      var data = new PriorityAccessData { UserId = userId, CreatedAt = DateTime.UtcNow };
      var r = db.PriorityAccesses.Add(data);
      await db.SaveChangesAsync();
      return r.Entity.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to add priority access for User '{UserId}'", userId);
      return e;
    }
  }

  public async Task<Result<Unit?>> Remove(string userId)
  {
    try
    {
      logger.LogInformation("Removing priority access for User '{UserId}'", userId);
      var data = await db.PriorityAccesses.Where(x => x.UserId == userId).FirstOrDefaultAsync();
      if (data == null)
        return (Unit?)null;
      db.PriorityAccesses.Remove(data);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to remove priority access for User '{UserId}'", userId);
      return e;
    }
  }

  public async Task<Result<bool>> Contains(string userId)
  {
    try
    {
      return await db.PriorityAccesses.AnyAsync(x => x.UserId == userId);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to check priority access for User '{UserId}'", userId);
      return e;
    }
  }
}

public static class PriorityDataMapper
{
  // Data -> Domain
  public static PriorityPolicyRecord ToRecord(this PriorityPolicyData data) =>
    new()
    {
      Name = data.Name,
      Allow = data.Allow,
      Target = data.Target?.ToTarget(),
      WindowStartSgt = data.WindowStartSgt,
      WindowEndSgt = data.WindowEndSgt,
      MinHoursToDeparture = data.MinHoursToDeparture,
      MaxHoursToDeparture = data.MaxHoursToDeparture,
      FeeKind = data.FeeKind == "Percent" ? PriorityFeeKind.Percent : PriorityFeeKind.Flat,
      FeeValue = data.FeeValue,
      SlotCap = data.SlotCap,
    };

  // Pre-unification rows -> equivalent unified rules, so old configuration
  // keeps behaving identically: the free target boosts at no charge, then
  // whoever had access (access target, else allow-all, else the allowlist)
  // pays the flat legacy fee; both inherit the legacy window and slot cap.
  // No settings row at all = the legacy defaults (fee 10, allowlist-only).
  public static List<PriorityPolicyRecord> SynthesizeLegacy(
    PrioritySettingsData? data,
    IReadOnlyList<string> allowlist
  )
  {
    var fee = data?.Fee ?? 10m;
    var allowAll = data?.AllowAll ?? false;
    var start = data?.WindowStartSgt;
    var end = data?.WindowEndSgt;
    var cap = data?.SlotCap;
    var rules = new List<PriorityPolicyRecord>();

    if (data?.FreeTarget != null)
      rules.Add(
        new PriorityPolicyRecord
        {
          Name = "Legacy: free boost",
          Allow = true,
          Target = data.FreeTarget.ToTarget(),
          WindowStartSgt = start,
          WindowEndSgt = end,
          FeeKind = PriorityFeeKind.Flat,
          FeeValue = 0m,
          SlotCap = cap,
        }
      );

    var accessTarget = data?.AccessTarget != null
      ? data.AccessTarget.ToTarget()
      : allowAll
        ? null
        : allowlist.Count > 0
          ? new DiscountTarget
          {
            MatchMode = DiscountMatchMode.Any,
            Matches = allowlist
              .Select(u => new DiscountMatch { Type = DiscountMatchType.UserId, Value = u })
              .ToList(),
          }
          : null;

    // allow-all and access-target rows always get an access rule; a pure
    // allowlist row only when somebody is actually allowlisted
    if (data?.AccessTarget != null || allowAll || allowlist.Count > 0)
      rules.Add(
        new PriorityPolicyRecord
        {
          Name = "Legacy: access",
          Allow = true,
          Target = accessTarget,
          WindowStartSgt = start,
          WindowEndSgt = end,
          FeeKind = PriorityFeeKind.Flat,
          FeeValue = fee,
          SlotCap = cap,
        }
      );

    return rules;
  }

  public static PriorityAccess ToDomain(this PriorityAccessData data) =>
    new() { UserId = data.UserId, CreatedAt = data.CreatedAt };

  // Domain -> Data: unified rows persist ONLY the policy list ('[]' when the
  // admin explicitly cleared it); the legacy columns are left at defaults and
  // never consulted for rows that carry a non-null Policies value
  public static PrioritySettingsData UpdateData(
    this PrioritySettingsData data,
    PrioritySettingsRecord record
  )
  {
    data.Policies = record.Policies.Select(p => p.ToData()).ToList();
    return data;
  }

  public static PriorityPolicyData ToData(this PriorityPolicyRecord record) =>
    new()
    {
      Name = record.Name,
      Allow = record.Allow,
      Target = record.Target?.ToData(),
      WindowStartSgt = record.WindowStartSgt,
      WindowEndSgt = record.WindowEndSgt,
      MinHoursToDeparture = record.MinHoursToDeparture,
      MaxHoursToDeparture = record.MaxHoursToDeparture,
      FeeKind = record.FeeKind == PriorityFeeKind.Percent ? "Percent" : "Flat",
      FeeValue = record.FeeValue,
      SlotCap = record.SlotCap,
    };
}
