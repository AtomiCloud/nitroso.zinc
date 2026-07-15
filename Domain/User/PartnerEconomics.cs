using CSharp_Result;

namespace Domain.User;

// Admin-facing users tagged through the case-insensitive "partner"
// ExtraRole. Email is normalized to an empty string by the read repository
// for the small number of legacy users that do not have one.
public record PartnerUser
{
  public required string Id { get; init; }

  public required string Username { get; init; }

  public required string Email { get; init; }
}

// Inclusive SGT calendar-date range. Each source is filtered on its own
// event timestamp before the daily subtotals are rolled into months.
public record PartnerPnlQuery
{
  public DateOnly? After { get; init; }

  public DateOnly? Before { get; init; }
}

// One DB-side source subtotal. UNION ALL emits sparse rows; booking rows are
// already monthly so passenger passports stay genuinely distinct for the
// month, while the pure calculator merges every source by SGT calendar month.
public record PartnerPnlDailySum
{
  public required DateOnly Date { get; init; }

  public required int Bookings { get; init; }

  // completed bookings with a consumed priority boost (Priority = true,
  // including FREE boosts whose PriorityFee snapshot is null) and what the
  // partner actually paid for them
  public required int BoostCount { get; init; }

  public required decimal BoostAmount { get; init; }

  // distinct non-null, non-empty passenger passport numbers across the
  // completed bookings represented by this subtotal
  public required int DistinctPassengers { get; init; }

  public required decimal Collected { get; init; }

  public required decimal KtmbCost { get; init; }

  public required decimal Deposits { get; init; }

  public required decimal WithdrawalGross { get; init; }

  public required decimal WithdrawalFeeIncome { get; init; }
}

// One non-empty SGT calendar month for a partner's wallet and bookings.
public record PartnerPnlRow
{
  // "MM-yyyy"
  public required string Month { get; init; }

  public required int Bookings { get; init; }

  public required decimal Collected { get; init; }

  public required decimal KtmbCost { get; init; }

  public required decimal Deposits { get; init; }

  public required decimal WithdrawalGross { get; init; }

  public required decimal WithdrawalFeeIncome { get; init; }

  // completed bookings that month with a consumed priority boost, and the
  // priority fees they kept — Bookings stays the full ticket count
  public required int BoostCount { get; init; }

  public required decimal BoostAmount { get; init; }

  // many distinct passenger passports on one account is a reselling signal
  public required int DistinctPassengers { get; init; }
}

public static class PartnerPnlCalculator
{
  public static PartnerPnlRow[] Analyze(
    IEnumerable<PartnerPnlDailySum> days,
    DateOnly? after,
    DateOnly? before
  ) =>
    [
      .. days
        .Where(d => (after is null || d.Date >= after) && (before is null || d.Date <= before))
        .GroupBy(d => new { d.Date.Year, d.Date.Month })
        .Select(g => new PartnerPnlRow
        {
          Month = new DateOnly(g.Key.Year, g.Key.Month, 1).ToString("MM-yyyy"),
          Bookings = g.Sum(d => d.Bookings),
          Collected = g.Sum(d => d.Collected),
          KtmbCost = g.Sum(d => d.KtmbCost),
          Deposits = g.Sum(d => d.Deposits),
          WithdrawalGross = g.Sum(d => d.WithdrawalGross),
          WithdrawalFeeIncome = g.Sum(d => d.WithdrawalFeeIncome),
          BoostCount = g.Sum(d => d.BoostCount),
          BoostAmount = g.Sum(d => d.BoostAmount),
          DistinctPassengers = g.Sum(d => d.DistinctPassengers),
        })
        .Where(r =>
          r.Bookings != 0
          || r.Collected != 0m
          || r.KtmbCost != 0m
          || r.Deposits != 0m
          || r.WithdrawalGross != 0m
          || r.WithdrawalFeeIncome != 0m
          || r.BoostCount != 0
          || r.BoostAmount != 0m
          || r.DistinctPassengers != 0
        )
        .OrderBy(r => r.Month[3..])
        .ThenBy(r => r.Month[..2]),
    ];
}

// Isolated read model: keeping these reporting queries out of IUserRepository
// avoids coupling the core user service and its many test fakes to analytics.
public interface IPartnerEconomicsRepository
{
  Task<Result<PartnerUser[]>> ListPartners();

  Task<Result<PartnerPnlRow[]>> Pnl(string userId, PartnerPnlQuery query);
}
