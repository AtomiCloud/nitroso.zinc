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

// One DB-side daily subtotal from one source. UNION ALL emits sparse rows;
// the pure calculator below merges them by SGT calendar month.
public record PartnerPnlDailySum
{
  public required DateOnly Date { get; init; }

  public required int Bookings { get; init; }

  // completed bookings with a consumed priority boost (Priority && a positive
  // PriorityFee — the same guard the analysis page uses) and their fee sum
  public required int BoostCount { get; init; }

  public required decimal BoostAmount { get; init; }

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
