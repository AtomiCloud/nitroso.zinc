using CSharp_Result;

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

  public static readonly PrioritySettingsRecord Default = new()
  {
    Fee = 10m,
    AllowAll = false,
    WindowStartSgt = null,
    WindowEndSgt = null,
  };
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
// user prioritize right now, and at what fee
public record PriorityEligibility
{
  public required bool Eligible { get; init; }

  public required decimal Fee { get; init; }
}

// Pure eligibility rules, shared by the endpoint, the prioritize guard and
// the unit tests
public static class PriorityRules
{
  // half-open [start, end); start > end wraps midnight; either bound null =
  // always open
  public static bool WindowOpen(TimeOnly? start, TimeOnly? end, TimeOnly nowSgt)
  {
    if (start == null || end == null)
      return true;
    return start.Value <= end.Value
      ? nowSgt >= start.Value && nowSgt < end.Value
      : nowSgt >= start.Value || nowSgt < end.Value;
  }

  public static bool Eligible(bool allowlisted, PrioritySettingsRecord settings, TimeOnly nowSgt)
  {
    return (allowlisted || settings.AllowAll)
      && WindowOpen(settings.WindowStartSgt, settings.WindowEndSgt, nowSgt);
  }
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
