using CSharp_Result;

namespace Domain.Invoice;

// The invoice's agreed terms — the handful of numbers that live in no other
// table because they describe the partnership rather than the product: the
// marketing profit share, the monthly infrastructure charge, and the rates at
// which out-of-system collections are recovered.
//
// Insert-only and effective-dated, exactly like the KTMB cost queue
// (Domain/Booking/KtmbCost.cs) and the withdrawal fee queue: the newest row
// whose EffectiveAt has passed is live, later rows are the queue, and history
// is never rewritten. That last part is the point — an invoice issued in July
// under a 50% share must still be explicable in December after the share
// changes, and the only way to keep that true is to never destroy the row it
// was computed under.
//
// UNLIKE the KTMB cost queue, "no effective row" is an ERROR here, not zero.
// A zero KTMB cost merely omits a component from an analysis; a zero share
// would silently pay the partners nothing and still look like a valid
// invoice. The schedule returns null and the caller must refuse.

public record InvoiceSettingsChange
{
  public required Guid Id { get; init; }

  // total share of net profit paid out to partners, as a percentage. Split
  // evenly across the effective partners (50% over two partners = 25% each).
  public required decimal MarketingSharePct { get; init; }

  // flat monthly infrastructure charge deducted before the split
  public required decimal Infrastructure { get; init; }

  // The partner-recovery rates. Partners sell boosts and tickets outside the
  // system and keep the cash; these are the assumed gross per unit, charged
  // back as an ADVANCE against their share (never as a business cost — see
  // the calculator).
  public required decimal RecoveryPerBoost { get; init; }

  public required decimal RecoveryPerTicket { get; init; }

  public required DateTime EffectiveAt { get; init; }

  public required DateTime CreatedAt { get; init; }
}

// which way a partner's half-cent goes when the pool cannot be split evenly
public enum InvoiceRoundingPreference : byte
{
  Down = 0,
  Up = 1,
}

// One partner's terms. Keyed by Suffix rather than by row id, because the
// same partner accumulates many rows over time and the invoice needs to know
// they are the same person.
public record InvoicePartnerChange
{
  public required Guid Id { get; init; }

  // stable identity across rows — the single letter on the invoice's
  // reference number (BB-2026-0801-C)
  public required string Suffix { get; init; }

  public required string Name { get; init; }

  public required InvoiceRoundingPreference RoundingPreference { get; init; }

  // A partner is removed by inserting an inactive row, never by deleting the
  // old ones. Deleting would silently change what an already-issued invoice
  // appears to have been computed from.
  public required bool Active { get; init; }

  // display and tie-break order. Load-bearing: the leftover-cent allocation
  // walks partners in a fixed order, so an unstable order would move a cent
  // between people between runs.
  public required int Position { get; init; }

  public required DateTime EffectiveAt { get; init; }

  public required DateTime CreatedAt { get; init; }
}

// the terms in force at one instant — what the calculator consumes
public record InvoiceTerms
{
  public required decimal MarketingSharePct { get; init; }

  public required decimal Infrastructure { get; init; }

  public required decimal RecoveryPerBoost { get; init; }

  public required decimal RecoveryPerTicket { get; init; }

  public required InvoicePartner[] Partners { get; init; }
}

public record InvoicePartner
{
  public required string Suffix { get; init; }

  public required string Name { get; init; }

  public required InvoiceRoundingPreference RoundingPreference { get; init; }
}

// current terms + the queued future changes, for the admin UI
public record InvoiceSettingsView
{
  // null when never configured — the UI shows "not set up", not zeroes
  public required InvoiceTerms? Current { get; init; }

  // The two halves of Current, reported separately so the UI can name the
  // half that is missing. Current is null whenever EITHER is — and "no terms"
  // is the wrong thing to tell an owner who saved terms but no partner yet
  // (production, 10 Oct 2026: four terms rows, zero partner rows).
  public InvoiceSettingsChange? CurrentSettings { get; init; }

  public InvoicePartner[] CurrentPartners { get; init; } = [];

  public required InvoiceSettingsChange[] Upcoming { get; init; }

  public required InvoicePartnerChange[] UpcomingPartners { get; init; }
}

public interface IInvoiceSettingsRepository
{
  // the full queue of both tables (a handful of admin-entered rows each)
  Task<Result<IEnumerable<InvoiceSettingsChange>>> ListSettings();

  Task<Result<IEnumerable<InvoicePartnerChange>>> ListPartners();

  Task<Result<InvoiceSettingsChange>> AddSettings(
    InvoiceSettingsChange change,
    DateTime? effectiveAt
  );

  Task<Result<InvoicePartnerChange>> AddPartner(
    InvoicePartnerChange change,
    DateTime? effectiveAt
  );
}

// Pure effective-dating math, shared by the endpoint, the calculator and the
// unit tests. Same newest-effective-first rule as KtmbCostSchedule, extended
// to a keyed set for partners.
public static class InvoiceSettingsSchedule
{
  // Ordering is (EffectiveAt, CreatedAt, Id) descending, identical to
  // KtmbCostSchedule: two rows queued for the same instant resolve by when
  // they were entered, and the id is the final deterministic tiebreak so the
  // answer never depends on row order coming back from the database.
  public static InvoiceSettingsChange? EffectiveSettings(
    IEnumerable<InvoiceSettingsChange> changes,
    DateTime at
  ) =>
    changes
      .Where(x => x.EffectiveAt <= at)
      .OrderByDescending(x => x.EffectiveAt)
      .ThenByDescending(x => x.CreatedAt)
      .ThenByDescending(x => x.Id)
      .FirstOrDefault();

  // The partner set in force at an instant: for each suffix independently,
  // the newest effective row wins; inactive ones then drop out. Resolving per
  // suffix (rather than taking the newest batch) means changing one partner's
  // rounding preference does not require re-entering the other partner.
  public static InvoicePartner[] EffectivePartners(
    IEnumerable<InvoicePartnerChange> changes,
    DateTime at
  ) =>
    changes
      .Where(x => x.EffectiveAt <= at)
      .GroupBy(x => x.Suffix, StringComparer.OrdinalIgnoreCase)
      .Select(g =>
        g.OrderByDescending(x => x.EffectiveAt)
          .ThenByDescending(x => x.CreatedAt)
          .ThenByDescending(x => x.Id)
          .First()
      )
      .Where(x => x.Active)
      .OrderBy(x => x.Position)
      .ThenBy(x => x.Suffix, StringComparer.Ordinal)
      .Select(x => new InvoicePartner
      {
        Suffix = x.Suffix,
        Name = x.Name,
        RoundingPreference = x.RoundingPreference,
      })
      .ToArray();

  // The terms in force, or null when they are not usable. Both halves are
  // required: settings with no partners would compute a share pool and have
  // nobody to allocate it to, which is a division by zero in the split rather
  // than a meaningful invoice.
  public static InvoiceTerms? EffectiveTerms(
    IEnumerable<InvoiceSettingsChange> settings,
    IEnumerable<InvoicePartnerChange> partners,
    DateTime at
  )
  {
    var s = EffectiveSettings(settings, at);
    if (s is null)
      return null;

    var p = EffectivePartners(partners, at);
    if (p.Length == 0)
      return null;

    return new InvoiceTerms
    {
      MarketingSharePct = s.MarketingSharePct,
      Infrastructure = s.Infrastructure,
      RecoveryPerBoost = s.RecoveryPerBoost,
      RecoveryPerTicket = s.RecoveryPerTicket,
      Partners = p,
    };
  }

  public static InvoiceSettingsView View(
    IEnumerable<InvoiceSettingsChange> settings,
    IEnumerable<InvoicePartnerChange> partners,
    DateTime now
  )
  {
    var allSettings = settings.ToArray();
    var allPartners = partners.ToArray();
    return new InvoiceSettingsView
    {
      Current = EffectiveTerms(allSettings, allPartners, now),
      CurrentSettings = EffectiveSettings(allSettings, now),
      CurrentPartners = EffectivePartners(allPartners, now),
      Upcoming = allSettings.Where(x => x.EffectiveAt > now).OrderBy(x => x.EffectiveAt).ToArray(),
      UpcomingPartners = allPartners
        .Where(x => x.EffectiveAt > now)
        .OrderBy(x => x.EffectiveAt)
        .ThenBy(x => x.Suffix, StringComparer.Ordinal)
        .ToArray(),
    };
  }
}

// Where a month's infrastructure charge came from.
public static class InfrastructureSource
{
  // every covered day had a settings row in force
  public const string Settings = "settings";

  // at least one covered day predates the first settings row and was charged
  // at the EARLIEST row instead. Production's first row took effect on
  // 10 Oct 2026 while the partnership (and the S$500/month charge) ran from
  // June, so a strict "nothing in force = 0" would silently drop the charge
  // from every month before the owner first saved the terms.
  public const string Assumed = "assumed";

  // no settings row exists at all: nothing is charged
  public const string None = "none";
}

// One month's infrastructure charge as the P&L deducts it.
public record InfrastructureCharge
{
  public required decimal Amount { get; init; }

  // SGT calendar days of the month the range covers (DaysInMonth for an
  // interior month, fewer at a partial range edge)
  public required int Days { get; init; }

  public required int DaysInMonth { get; init; }

  // InfrastructureSource.*
  public required string Source { get; init; }
}

// The flat monthly infrastructure charge spread over an arbitrary SGT date
// range, for the P&L. Day-weighted: each covered SGT day costs
// (Infrastructure in force at the END of that day) / days-in-month. So an
// interior month with one rate costs exactly that rate, a partial edge month
// is prorated by the days the range covers, and a mid-month change is charged
// each rate for the days it was in force. Same newest-effective-first rule as
// EffectiveSettings — this is a sum of EffectiveSettings over days.
public static class InfrastructureSchedule
{
  private static readonly TimeSpan SgtOffset = TimeSpan.FromHours(8);

  // the last instant of an SGT calendar day, in UTC
  public static DateTime DayEndUtc(DateOnly day) =>
    DateTime.SpecifyKind(
      day.AddDays(1).ToDateTime(TimeOnly.MinValue).Subtract(SgtOffset).AddTicks(-1),
      DateTimeKind.Utc
    );

  // first..last are the covered days of ONE month, inclusive (callers clamp
  // the range to the month); an empty or inverted span charges nothing
  public static InfrastructureCharge ForDays(
    IEnumerable<InvoiceSettingsChange> changes,
    DateOnly first,
    DateOnly last
  )
  {
    var all = changes.ToArray();
    var daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);
    var earliest = all.OrderBy(x => x.EffectiveAt)
      .ThenBy(x => x.CreatedAt)
      .ThenBy(x => x.Id)
      .FirstOrDefault();
    if (earliest is null || last < first)
      return new InfrastructureCharge
      {
        Amount = 0m,
        Days = Math.Max(0, last.DayNumber - first.DayNumber + 1),
        DaysInMonth = daysInMonth,
        Source = earliest is null ? InfrastructureSource.None : InfrastructureSource.Settings,
      };

    var total = 0m;
    var assumed = false;
    for (var d = first; d <= last; d = d.AddDays(1))
    {
      var s = InvoiceSettingsSchedule.EffectiveSettings(all, DayEndUtc(d));
      if (s is null)
        assumed = true;
      total += (s ?? earliest).Infrastructure;
    }

    return new InfrastructureCharge
    {
      Amount = Math.Round(total / daysInMonth, 2, MidpointRounding.AwayFromZero),
      Days = last.DayNumber - first.DayNumber + 1,
      DaysInMonth = daysInMonth,
      Source = assumed ? InfrastructureSource.Assumed : InfrastructureSource.Settings,
    };
  }
}
