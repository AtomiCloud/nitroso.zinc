namespace Domain.Invoice;

// The money on an invoice, flattened.
//
// This is the comparison surface for drift, and it is deliberately small.
// Two things follow from that:
//
//   - A frozen invoice does not have to be reconstituted into the full
//     computed tree to be checked. Pulling ~14 figures out of the stored JSON
//     is enough, so the deep record tree never needs a reverse mapper whose
//     only caller would be this check.
//   - The check reports what a partner would notice on their statement. An
//     intermediate that moves without moving any of these is not something
//     anyone needs to act on.
public record InvoiceFigures
{
  public required decimal FxRate { get; init; }

  public required decimal FeeRatePct { get; init; }

  public required int Tickets { get; init; }

  public required decimal Revenue { get; init; }

  public required decimal PricePerTicket { get; init; }

  public required decimal DirectCost { get; init; }

  public required decimal Contribution { get; init; }

  public required decimal WastedFee { get; init; }

  public required decimal NetProfit { get; init; }

  public required decimal ShareBase { get; init; }

  public required decimal MarketingSharePool { get; init; }

  // keyed by partner suffix, because a reordered partner list must not read
  // as every partner's amount having changed
  public required IReadOnlyDictionary<string, InvoicePartnerFigures> Shares { get; init; }

  public static InvoiceFigures Of(InvoiceComputed c) =>
    new()
    {
      FxRate = c.Fx.FxRate,
      FeeRatePct = c.Fee.FeeRatePct,
      Tickets = c.Totals.Tickets,
      Revenue = c.Totals.Revenue,
      PricePerTicket = c.Totals.PricePerTicket,
      DirectCost = c.Totals.DirectCost,
      Contribution = c.Totals.Contribution,
      WastedFee = c.Adjustments.WastedFee,
      NetProfit = c.Result.NetProfit,
      ShareBase = c.Result.ShareBase,
      MarketingSharePool = c.Result.MarketingSharePool,
      Shares = c.Result.Shares.ToDictionary(
        s => s.Suffix,
        s => new InvoicePartnerFigures { Amount = s.Amount, Advance = s.Advance },
        StringComparer.OrdinalIgnoreCase
      ),
    };
}

public record InvoicePartnerFigures
{
  public required decimal Amount { get; init; }

  public required decimal Advance { get; init; }
}

// Re-runs today's engine over an invoice's frozen inputs and reports where
// the answer changed.
//
// This exists because freezing hides engine bugs by design. Once an invoice
// is issued it renders from stored figures, so a later correction to the
// calculator is invisible on exactly the documents most worth checking. The
// drift check makes that visible without touching the frozen figures.
//
// Deliberately NOT here: any notion of "correcting" the invoice. The frozen
// figures are what was paid. Drift is a prompt for a human decision (reissue,
// credit note, leave it), and this project has already had one — the June
// fare restatement — where "leave it" was the right answer.
public static class InvoiceDriftCheck
{
  // The caller supplies the frozen figures (read out of the stored document)
  // and the frozen inputs; the current engine is run here.
  public static InvoiceDrift Compare(
    Guid id,
    int frozenEngineVersion,
    InvoiceFigures frozen,
    InvoiceMonthInput frozenInputs
  )
  {
    var current = InvoiceFigures.Of(InvoiceCalculator.Compute(frozenInputs));
    return new InvoiceDrift
    {
      Id = id,
      FrozenEngineVersion = frozenEngineVersion,
      CurrentEngineVersion = InvoiceEngine.Version,
      Fields = Diff(frozen, current).ToArray(),
    };
  }

  private static IEnumerable<InvoiceDriftField> Diff(InvoiceFigures a, InvoiceFigures b)
  {
    foreach (var f in Compare("fx.fxRate", a.FxRate, b.FxRate))
      yield return f;
    foreach (var f in Compare("fee.feeRatePct", a.FeeRatePct, b.FeeRatePct))
      yield return f;
    foreach (var f in Compare("totals.tickets", a.Tickets, b.Tickets))
      yield return f;
    foreach (var f in Compare("totals.revenue", a.Revenue, b.Revenue))
      yield return f;
    foreach (var f in Compare("totals.pricePerTicket", a.PricePerTicket, b.PricePerTicket))
      yield return f;
    foreach (var f in Compare("totals.directCost", a.DirectCost, b.DirectCost))
      yield return f;
    foreach (var f in Compare("totals.contribution", a.Contribution, b.Contribution))
      yield return f;
    foreach (var f in Compare("adjustments.wastedFee", a.WastedFee, b.WastedFee))
      yield return f;
    foreach (var f in Compare("result.netProfit", a.NetProfit, b.NetProfit))
      yield return f;
    foreach (var f in Compare("result.shareBase", a.ShareBase, b.ShareBase))
      yield return f;
    foreach (var f in Compare("result.marketingSharePool", a.MarketingSharePool, b.MarketingSharePool))
      yield return f;

    foreach (var (suffix, frozenShare) in a.Shares.OrderBy(kv => kv.Key, StringComparer.Ordinal))
    {
      // A partner paid on the frozen invoice and absent now is reported as
      // their amount going to zero — what it would mean if this were applied.
      var currentShare =
        b.Shares.TryGetValue(suffix, out var s)
          ? s
          : new InvoicePartnerFigures { Amount = 0m, Advance = 0m };

      foreach (var f in Compare($"shares.{suffix}.amount", frozenShare.Amount, currentShare.Amount))
        yield return f;
      foreach (var f in Compare($"shares.{suffix}.advance", frozenShare.Advance, currentShare.Advance))
        yield return f;
    }

    foreach (var (suffix, added) in b.Shares.Where(kv => !a.Shares.ContainsKey(kv.Key))
      .OrderBy(kv => kv.Key, StringComparer.Ordinal))
    {
      yield return new InvoiceDriftField
      {
        Path = $"shares.{suffix}.amount",
        Frozen = 0m,
        Current = added.Amount,
      };
    }
  }

  // Exact equality on purpose. Every figure here is a decimal produced by the
  // same rounding function, so equal inputs through an unchanged engine give
  // identical results; any tolerance would only serve to hide a real change
  // of a cent, and a cent is the unit this whole system argues in.
  private static IEnumerable<InvoiceDriftField> Compare(string path, decimal frozen, decimal current)
  {
    if (frozen != current)
      yield return new InvoiceDriftField { Path = path, Frozen = frozen, Current = current };
  }
}
