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
      Upcoming = allSettings.Where(x => x.EffectiveAt > now).OrderBy(x => x.EffectiveAt).ToArray(),
      UpcomingPartners = allPartners
        .Where(x => x.EffectiveAt > now)
        .OrderBy(x => x.EffectiveAt)
        .ThenBy(x => x.Suffix, StringComparer.Ordinal)
        .ToArray(),
    };
  }
}
