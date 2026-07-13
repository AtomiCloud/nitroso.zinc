using CSharp_Result;
using Domain.Discount;

namespace Domain.Booking;

// THE priority-queue configuration: one ordered policy list, nothing else.
// Rules are evaluated top to bottom; the FIRST rule whose conditions all
// match decides everything (access, fee, slot cap); no matching rule = deny.
// Insert-only storage, newest row wins (same pattern as Cost). Rows written
// before unification are synthesized into equivalent rules by the data layer,
// so legacy fee/allow-all/allowlist/target/window settings keep behaving
// identically until an admin saves the new shape.
public record PrioritySettingsRecord
{
  public required IReadOnlyList<PriorityPolicyRecord> Policies { get; init; }

  public static readonly PrioritySettingsRecord Default = new() { Policies = [] };
}

public enum PriorityFeeKind
{
  // FeeValue is SGD
  Flat,

  // FeeValue is a percentage of the booking's charged ticket amount
  Percent,
}

// One rule. Conditions (all unset conditions are wildcards):
// - Target: who (user ids / roles, the discount-target matcher)
// - WindowStartSgt/WindowEndSgt: SGT wall-clock window (may wrap midnight)
// - Min/MaxHoursToDeparture: hours until the timeslot departs, [Min, Max)
// Decision (Allow rules only):
// - FeeKind/FeeValue: what to charge (flat SGD or % of ticket; 0 = free)
// - SlotCap: max queued priority bookings in the timeslot (null = uncapped)
public record PriorityPolicyRecord
{
  public required string Name { get; init; }

  public required bool Allow { get; init; }

  public DiscountTarget? Target { get; init; }

  public TimeOnly? WindowStartSgt { get; init; }

  public TimeOnly? WindowEndSgt { get; init; }

  public decimal? MinHoursToDeparture { get; init; }

  public decimal? MaxHoursToDeparture { get; init; }

  public PriorityFeeKind FeeKind { get; init; } = PriorityFeeKind.Flat;

  public decimal FeeValue { get; init; }

  public int? SlotCap { get; init; }
}

// What the eligibility endpoints (and the prioritize guard) answer. Fee is
// null when it cannot be known yet (a percent rule matched without a booking
// in scope) — never null when Eligible on the booking-scoped path. SlotCap/
// SlotsLeft only when the matched rule caps the timeslot and a booking is in
// scope. PolicyName = the matched rule, for admin debugging.
public record PriorityEligibility
{
  public required bool Eligible { get; init; }

  public required decimal? Fee { get; init; }

  public required bool Free { get; init; }

  public int? SlotCap { get; init; }

  public int? SlotsLeft { get; init; }

  public string? PolicyName { get; init; }
}

// Pure rule evaluation, shared by the endpoints, the prioritize guard and
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

  // The first rule whose conditions all match, or null (= deny).
  // hoursToDeparture = null means "no booking in scope" (the generic
  // eligibility endpoint) — hour-bounded rules are then SKIPPED, so the
  // generic answer only reflects rules that hold for every timeslot.
  public static PriorityPolicyRecord? Match(
    IReadOnlyList<PriorityPolicyRecord> policies,
    TimeOnly nowSgt,
    decimal? hoursToDeparture,
    string userId,
    string[] roles
  )
  {
    foreach (var p in policies)
    {
      if (!WindowOpen(p.WindowStartSgt, p.WindowEndSgt, nowSgt))
        continue;
      if (p.Target != null && !TargetMatcher.Matches(p.Target, userId, roles))
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
      return p;
    }
    return null;
  }

  // The fee an Allow rule charges: flat = the value itself; percent = that
  // share of the booking's charged ticket amount (null when no booking is in
  // scope yet), rounded to cents
  public static decimal? Fee(PriorityPolicyRecord rule, decimal? ticketAmount) =>
    rule.FeeKind switch
    {
      PriorityFeeKind.Flat => rule.FeeValue,
      PriorityFeeKind.Percent => ticketAmount == null
        ? null
        : Math.Round(
          rule.FeeValue / 100m * Math.Abs(ticketAmount.Value),
          2,
          MidpointRounding.AwayFromZero
        ),
      _ => null,
    };
}

public interface IPrioritySettingsRepository
{
  // the current unified policy list: the newest row's policies, or — for
  // rows/installations predating unification — rules synthesized from the
  // legacy fields + allowlist so behavior is unchanged
  Task<Result<PrioritySettingsRecord>> GetCurrent();

  Task<Result<PrioritySettingsPrincipal>> Create(PrioritySettingsRecord record);
}

public record PrioritySettingsPrincipal
{
  public required Guid Id { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required PrioritySettingsRecord Record { get; init; }
}

// A user allowed to prioritize bookings — LEGACY (pre-unification): consulted
// only when synthesizing rules from a pre-unification settings row; the
// endpoints remain for compatibility but the admin UI no longer manages it
public record PriorityAccess
{
  public required string UserId { get; init; }

  public required DateTime CreatedAt { get; init; }
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
