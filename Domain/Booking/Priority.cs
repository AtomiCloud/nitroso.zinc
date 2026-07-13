using CSharp_Result;
using Domain.Discount;

namespace Domain.Booking;

// Admin-editable priority-queue settings (insert-only, newest row wins —
// same pattern as Cost). With no row the defaults apply.
public record PrioritySettingsRecord
{
  public required decimal Fee { get; init; }

  // true = every user may prioritize; false = allowlisted users only
  public required bool AllowAll { get; init; }

  // SGT wall-clock window [WindowStartSgt, WindowEndSgt) in which
  // prioritizing is available; null = always available. May wrap midnight
  // (start > end).
  public required TimeOnly? WindowStartSgt { get; init; }

  public required TimeOnly? WindowEndSgt { get; init; }

  // Who gets the priority boost FREE (fee 0, no ledger row) — matched against
  // the user's id and PRICING roles (JWT roles ∪ admin-granted ExtraRoles,
  // the same union discount targeting prices with). null = nobody is free.
  // Same shape and semantics as discount targeting (All/Any/None).
  public DiscountTarget? FreeTarget { get; init; }

  // Who MAY use priority at all — richer replacement for AllowAll + the
  // allowlist. When set it takes PRECEDENCE over AllowAll/PriorityAccessData;
  // null = legacy behavior (allowlisted OR AllowAll) unchanged.
  public DiscountTarget? AccessTarget { get; init; }

  // Ordered policy chain evaluated BEFORE AccessTarget/AllowAll: the first
  // rule whose conditions all match decides (see PriorityPolicyRecord); no
  // matching rule falls through to the legacy gate. Empty = no policies.
  public IReadOnlyList<PriorityPolicyRecord> Policies { get; init; } = [];

  // Max priority bookings per timeslot (direction+date+time), counted over
  // the queued statuses; null = uncapped. Gives buyers predictability: a
  // boost can never be diluted below 1-in-SlotCap odds.
  public int? SlotCap { get; init; }

  public static readonly PrioritySettingsRecord Default = new()
  {
    Fee = 10m,
    AllowAll = false,
    WindowStartSgt = null,
    WindowEndSgt = null,
    FreeTarget = null,
    AccessTarget = null,
    Policies = [],
    SlotCap = null,
  };
}

// One rule in the ordered priority policy chain. A rule APPLIES when every
// set condition matches (unset = wildcard): Target (same shape as discount
// targeting, matched against the user's id and pricing roles) and the
// hours-to-departure interval [Min, Max). The first applying rule decides:
// Allow (optionally at FeeOverride instead of the base fee) or deny.
// Examples — "boosting only opens inside 48h": deny rule with Min=48;
// "VIPs may boost anytime, everyone else only outside 6h": allow rule with
// Target=vip, then deny rule with Max=6.
public record PriorityPolicyRecord
{
  public required string Name { get; init; }

  public required bool Allow { get; init; }

  public DiscountTarget? Target { get; init; }

  public decimal? MinHoursToDeparture { get; init; }

  public decimal? MaxHoursToDeparture { get; init; }

  // Allow rules only: charge this instead of the base fee (FreeTarget still
  // wins and makes the boost free)
  public decimal? FeeOverride { get; init; }
}

public record PrioritySettingsPrincipal
{
  public required Guid Id { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required PrioritySettingsRecord Record { get; init; }
}

// A user allowed to prioritize bookings (when AllowAll is off)
public record PriorityAccess
{
  public required string UserId { get; init; }

  public required DateTime CreatedAt { get; init; }
}

// What the eligibility endpoint (and the prioritize guard) answers: may this
// user prioritize right now, at what fee, and whether the boost is free for
// them (Free => Fee is 0). SlotCap/SlotsLeft are only known when the check
// is scoped to a booking (its timeslot); null otherwise or when uncapped.
public record PriorityEligibility
{
  public required bool Eligible { get; init; }

  public required decimal Fee { get; init; }

  public required bool Free { get; init; }

  public int? SlotCap { get; init; }

  public int? SlotsLeft { get; init; }
}

// Pure eligibility rules, shared by the endpoint, the prioritize guard and
// the unit tests
public static class PriorityRules
{
  // half-open [start, end); start > end wraps midnight; either bound null =
  // always open. start == end is ALSO always open: an admin writing
  // 00:00 -> 00:00 means "all day", and the strict half-open reading would
  // silently brick the feature
  public static bool WindowOpen(TimeOnly? start, TimeOnly? end, TimeOnly nowSgt)
  {
    if (start == null || end == null || start.Value == end.Value)
      return true;
    return start.Value < end.Value
      ? nowSgt >= start.Value && nowSgt < end.Value
      : nowSgt >= start.Value || nowSgt < end.Value;
  }

  // May this user prioritize: AccessTarget (when configured) REPLACES the
  // legacy allowlist/AllowAll gate; otherwise legacy semantics apply. The SGT
  // availability window gates both paths.
  public static bool Eligible(
    bool allowlisted,
    PrioritySettingsRecord settings,
    TimeOnly nowSgt,
    string userId = "",
    string[]? roles = null
  )
  {
    var access =
      settings.AccessTarget != null
        ? Discount.TargetMatcher.Matches(settings.AccessTarget, userId, roles ?? [])
        : allowlisted || settings.AllowAll;
    return access && WindowOpen(settings.WindowStartSgt, settings.WindowEndSgt, nowSgt);
  }

  // Full evaluation: the SGT window gates everything; then the policy chain
  // runs first-match-wins; no matching rule falls through to Eligible()'s
  // legacy gate. hoursToDeparture = null means "no booking in scope" (the
  // generic eligibility endpoint) — hour-bounded rules are then SKIPPED, so
  // the generic answer only reflects rules that hold for every timeslot.
  public static (bool Eligible, decimal Fee) Decide(
    bool allowlisted,
    PrioritySettingsRecord settings,
    TimeOnly nowSgt,
    decimal? hoursToDeparture,
    string userId,
    string[] roles
  )
  {
    if (!WindowOpen(settings.WindowStartSgt, settings.WindowEndSgt, nowSgt))
      return (false, settings.Fee);

    foreach (var p in settings.Policies)
    {
      if (p.Target != null && !Discount.TargetMatcher.Matches(p.Target, userId, roles))
        continue;
      if (p.MinHoursToDeparture != null || p.MaxHoursToDeparture != null)
      {
        if (hoursToDeparture == null)
          continue;
        if (p.MinHoursToDeparture != null && hoursToDeparture < p.MinHoursToDeparture)
          continue;
        if (p.MaxHoursToDeparture != null && hoursToDeparture >= p.MaxHoursToDeparture)
          continue;
      }
      return (p.Allow, p.Allow ? p.FeeOverride ?? settings.Fee : settings.Fee);
    }

    return (Eligible(allowlisted, settings, nowSgt, userId, roles), settings.Fee);
  }

  // Is the boost free for this user: FreeTarget matched against the user's
  // id and pricing roles; no target = never free
  public static bool Free(PrioritySettingsRecord settings, string userId, string[] roles) =>
    settings.FreeTarget != null
    && Discount.TargetMatcher.Matches(settings.FreeTarget, userId, roles);
}

public interface IPrioritySettingsRepository
{
  // the newest settings row, or null when none was ever written (defaults apply)
  Task<Result<PrioritySettingsPrincipal?>> GetCurrent();

  Task<Result<PrioritySettingsPrincipal>> Create(PrioritySettingsRecord record);
}

public interface IPriorityAccessRepository
{
  Task<Result<IEnumerable<PriorityAccess>>> List();

  // idempotent: adding an already-allowlisted user returns the existing row
  Task<Result<PriorityAccess>> Add(string userId);

  // null when the user was not allowlisted
  Task<Result<Unit?>> Remove(string userId);

  Task<Result<bool>> Contains(string userId);
}
