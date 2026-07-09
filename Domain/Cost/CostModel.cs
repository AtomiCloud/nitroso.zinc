using Domain.Discount;
using Domain.Timings;

namespace Domain.Cost;

public record CostPrincipal
{
  public required Guid Id { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required CostRecord Record { get; init; }
}

public record CostRecord
{
  public required decimal Cost { get; init; }
}

// The booking dimensions pricing policies match on — a lightweight spec so
// callers (e.g. the purchase-page live preview) can price a booking without
// fabricating passenger details
public record BookingCostSpec
{
  public required DateOnly Date { get; init; }

  public required TimeOnly Time { get; init; }

  public required TrainDirection Direction { get; init; }
}

// A pricing rule: when every non-null Match* dimension equals the booking's,
// the (signed) Amount is added to the base cost — negative = discount,
// IsPercentage = percent of the base cost instead of a flat SGD amount
public record CostPolicyRecord
{
  public required string Name { get; init; }

  public required bool Enabled { get; init; }

  // null = matches any value for that dimension (all null = matches everything)
  public required DateOnly? MatchDate { get; init; }

  public required TimeOnly? MatchTime { get; init; }

  public required DayOfWeek? MatchDayOfWeek { get; init; }

  public required TrainDirection? MatchDirection { get; init; }

  // applies only when the booking is made this close (or closer) to departure
  public required int? LeadTimeUnderHours { get; init; }

  // SIGNED: negative = discount; percent of base cost when IsPercentage
  public required decimal Amount { get; init; }

  public required bool IsPercentage { get; init; }

  // active window [EffectiveAt, ExpiresAt); null = unbounded on that side
  public required DateTime? EffectiveAt { get; init; }

  public required DateTime? ExpiresAt { get; init; }
}

public record CostPolicyPrincipal
{
  public required Guid Id { get; init; }

  public required DateTime CreatedAt { get; init; }

  public required CostPolicyRecord Record { get; init; }
}

// One applied policy's contribution to the price breakdown
public record CostPolicyLine
{
  public required string Name { get; init; }

  // the delta actually added (already resolved from percentage where needed)
  public required decimal Delta { get; init; }
}

// The full price breakdown: Cost (base) + PolicyLines = Subtotal (floored at
// 0), then Discounts bring Subtotal down to Final — every step adds up
public record MaterializedCost
{
  public required decimal Cost { get; init; }

  public required IEnumerable<CostPolicyLine> PolicyLines { get; init; }

  public required decimal Subtotal { get; init; }

  public required decimal Final { get; init; }

  public required IEnumerable<DiscountRecord> Discounts { get; init; }
}
