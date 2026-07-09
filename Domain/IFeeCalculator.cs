using CSharp_Result;

namespace Domain;

public enum FeeType
{
  Withdrawal = 0,
  Deposit = 1,
  Termination = 2,
}

// A fee specification: flat component plus a percentage of the amount, with
// an optional absolute cap. Rates default to zero — no fee exists until an
// admin queues one.
public record FeeSpec
{
  public required decimal Percentage { get; init; }

  public required decimal FlatAmount { get; init; }

  // absolute ceiling on the computed fee; null = uncapped
  public decimal? Cap { get; init; }

  public static readonly FeeSpec None = new() { Percentage = 0m, FlatAmount = 0m };
}

// A queued (or past) fee change event
public record FeeChange
{
  public required Guid Id { get; init; }

  public required FeeType Type { get; init; }

  public required decimal Percentage { get; init; }

  public required decimal FlatAmount { get; init; }

  // absolute ceiling on the computed fee; null = uncapped
  public decimal? Cap { get; init; }

  public required DateTime EffectiveAt { get; init; }
}

// The live fee. Async because rates are admin-editable at runtime (an
// insert-only queue of changes; the newest effective row wins, zero-zero
// while the queue has no effective row).
public interface IFeeCalculator
{
  Task<Result<FeeSpec>> Current(FeeType type);

  // round-to-even cents of flat + percentage x amount, capped at the amount
  // so a fee can never exceed what is being moved, and at the admin-set Cap
  // when one exists
  Task<Result<decimal>> Compute(FeeType type, decimal amount);
}

// Admin mutation surface for the fee queue
public interface IFeeRepository
{
  // the change currently in effect for the type, or null when none ever was
  Task<Result<FeeChange?>> GetCurrent(FeeType type);

  // changes scheduled in the future for the type, soonest first
  Task<Result<IEnumerable<FeeChange>>> GetUpcoming(FeeType type);

  // effectiveAt null = immediate; cap null = uncapped
  Task<Result<FeeChange>> Add(
    FeeType type,
    decimal percentage,
    decimal flatAmount,
    decimal? cap,
    DateTime? effectiveAt
  );

  // cancel a QUEUED change (only rows still in the future may be removed —
  // effective history is immutable); null when no such queued row exists
  Task<Result<Domain.FeeChange?>> CancelUpcoming(Guid id);
}
