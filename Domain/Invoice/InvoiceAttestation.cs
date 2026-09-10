namespace Domain.Invoice;

// The check that stands between a paper invoice and a row claiming to be it.
//
// WHY THIS EXISTS. Transcribing June, July and August means hand-copying ~25
// input figures out of a JSON file the invoices/ toolchain produced. Every one
// of those is an opportunity to fat-finger a digit, and the result would not
// look wrong: the engine would happily compute a complete, internally
// consistent invoice from the bad input and store it as an ISSUED document —
// the strongest claim this system can make about money. Nobody re-reads a
// settled month.
//
// So the caller states, separately, what the document it is holding actually
// says was paid. The server computes from the inputs and compares. If the two
// disagree the transcription is refused, and the operator finds out at the
// moment they can still go and look at the PDF.
//
// FOUR FIGURES, NOT FORTY. Every intermediate is downstream of these, so an
// input error that leaves tickets, revenue, net profit and every partner
// amount intact did not change what anyone was paid. Demanding forty numbers
// off a PDF would make the backfill tedious enough to be done carelessly,
// which is the failure this is here to prevent.
//
// EXACT EQUALITY, NOT A TOLERANCE. These are decimals against figures printed
// to the cent, and the engine already reproduces all three months exactly (see
// InvoiceCalculatorGoldenFixtureTests). A tolerance here would only ever serve
// to let a real error through.
public record InvoiceAttestation
{
  public required int Tickets { get; init; }

  public required decimal Revenue { get; init; }

  public required decimal NetProfit { get; init; }

  // partner suffix -> the amount actually transferred to that partner
  public required IReadOnlyDictionary<string, decimal> Amounts { get; init; }
}

public record InvoiceAttestationMismatch
{
  public required string Field { get; init; }

  // what the operator says the document says
  public required decimal Attested { get; init; }

  // what the engine gets from the inputs supplied alongside it
  public required decimal Computed { get; init; }
}

public static class InvoiceAttestationCheck
{
  // Empty means the transcription reproduces the document.
  public static IReadOnlyList<InvoiceAttestationMismatch> Compare(
    InvoiceAttestation attested,
    InvoiceComputed computed
  )
  {
    var m = new List<InvoiceAttestationMismatch>();

    Add(m, "tickets", attested.Tickets, computed.Totals.Tickets);
    Add(m, "revenue", attested.Revenue, computed.Totals.Revenue);
    Add(m, "netProfit", attested.NetProfit, computed.Result.NetProfit);

    // Both directions are checked. A partner present in the attestation but
    // not in the computed shares means the partner list in the inputs is
    // wrong, and the reverse means the operator forgot someone who was paid —
    // and a missing partner is exactly the error that is invisible in a total
    // that still adds up.
    foreach (var (suffix, amount) in attested.Amounts)
    {
      var share = computed.Result.Shares.FirstOrDefault(s =>
        string.Equals(s.Suffix, suffix, StringComparison.OrdinalIgnoreCase)
      );
      if (share is null)
      {
        m.Add(
          new InvoiceAttestationMismatch
          {
            Field = $"shares.{suffix}.amount",
            Attested = amount,
            // Reported as 0 rather than omitted: the operator asked what this
            // partner was paid, and the answer the engine gives is nothing.
            Computed = 0m,
          }
        );
        continue;
      }

      Add(m, $"shares.{suffix}.amount", amount, share.Amount);
    }

    foreach (var share in computed.Result.Shares)
    {
      var claimed = attested.Amounts.Any(kv =>
        string.Equals(kv.Key, share.Suffix, StringComparison.OrdinalIgnoreCase)
      );
      if (!claimed)
      {
        m.Add(
          new InvoiceAttestationMismatch
          {
            Field = $"shares.{share.Suffix}.amount",
            Attested = 0m,
            Computed = share.Amount,
          }
        );
      }
    }

    return m;
  }

  private static void Add(
    ICollection<InvoiceAttestationMismatch> into,
    string field,
    decimal attested,
    decimal computed
  )
  {
    if (attested != computed)
    {
      into.Add(
        new InvoiceAttestationMismatch
        {
          Field = field,
          Attested = attested,
          Computed = computed,
        }
      );
    }
  }
}
