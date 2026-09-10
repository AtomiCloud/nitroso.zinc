using System.Globalization;
using System.Text;
using Domain.Invoice;

namespace App.Modules.Invoices.API.V1;

// The 4-page invoice document: cover, appendix (how the blended rates were
// derived), the single-total table, then basis & assumptions with data
// provenance. Ported line-for-line from invoices/render.ts, which produced the
// June, July and August PDFs that were actually sent and paid.
//
// WHY HTML AND NOT A PDF LIBRARY.
//
// render.ts makes its PDFs by handing this exact markup and this exact
// stylesheet to headless Chrome. The stylesheet is already `@page { size: A4 }`
// and already paginates. A browser opening this response and pressing
// Ctrl+P -> Save as PDF is therefore THE SAME ENGINE ON THE SAME STYLESHEET,
// for zero new dependencies.
//
// The alternatives were considered and rejected:
//   - Headless Chrome inside the zinc container: +300-400 MB of image, and a
//     browser process in the blast radius of a financial API.
//   - QuestPDF: rewrites all 460 lines of layout, discarding visual
//     equivalence with the documents already on file, plus a licensing call.
//   - Client-side JS PDF in argon: argon's first such dependency, in a
//     codebase with no component tests.
//
// FIDELITY IS THE POINT. A partner has three of these on file. A regenerated
// June must look like the June they were sent, or the difference reads as a
// restatement of money that was already settled. So this is a transliteration,
// not a redesign — including the parts that would be written differently
// today.
//
// One deliberate departure: render.ts hardcodes `BB-2026-` in the invoice
// number. That is correct for every invoice issued so far and silently wrong
// from January onward, and this endpoint exists precisely so invoices can be
// generated in later years. The year is derived from the period instead, which
// reproduces "2026" for all three issued months.
public static class InvoiceHtml
{
  // ---- formatting ---------------------------------------------------------
  //
  // The TypeScript formats with toLocaleString("en-SG"), which for these
  // numbers is comma grouping and a period decimal point — identical to the
  // invariant culture. Invariant is used rather than en-SG so the output can
  // never depend on which ICU data the container happens to ship.

  private static string N2(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);

  private static string N0(decimal v) => v.ToString("N0", CultureInfo.InvariantCulture);

  private static string F(decimal v, int dp) =>
    v.ToString("F" + dp.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

  // Percentages that read as labels rather than measurements ("25% of net
  // profit"). Trailing zeros are trimmed so a half share prints "25", not
  // "25.00", matching how the TypeScript interpolates a plain number.
  private static string Pct(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

  private static string Esc(string s) =>
    s.Replace("&", "&amp;", StringComparison.Ordinal)
      .Replace("<", "&lt;", StringComparison.Ordinal)
      .Replace(">", "&gt;", StringComparison.Ordinal);

  // Rounding for figures derived at render time rather than by the calculator.
  // Same rule as the engine — see Domain/Invoice/InvoiceRounding.cs.
  private static decimal R2(decimal v) => InvoiceRounding.R(v);

  // First word of "June 2026". Used wherever the prose needs a bare month.
  private static string MonthWord(InvoiceComputed c) =>
    c.Input.Period.MonthName.Split(' ')[0];

  // The year the invoice number carries. Taken from the period rather than
  // hardcoded; falls back to the issue date, then to today, so a malformed
  // month name degrades to a plausible number rather than throwing while
  // rendering a document.
  private static string Year(InvoiceComputed c)
  {
    foreach (
      var token in c.Input.Period.MonthName.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
      )
    )
    {
      if (token.Length == 4 && int.TryParse(token, out var y) && y is > 1900 and < 3000)
        return y.ToString(CultureInfo.InvariantCulture);
    }

    if (
      DateOnly.TryParseExact(
        c.Input.IssueDate,
        InvoiceMapper.DateFormat,
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var issue
      )
    )
      return issue.Year.ToString(CultureInfo.InvariantCulture);

    return DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
  }

  private static string Reference(InvoiceComputed c, InvoiceShare p) =>
    $"BB-{Year(c)}-{Esc(c.Input.Period.Seq)}-{Esc(p.Suffix)}";

  // True when the month has any ancillary activity at all. June 2026 predates
  // every one of these streams, and its invoices are already issued and paid —
  // so the section is omitted entirely rather than printed as a wall of zeros,
  // keeping a regenerated June identical to the documents on file.
  private static bool HasAncillary(InvoiceComputed c)
  {
    var an = c.Ancillary;
    return an.Priority.Gross > 0
      || an.Priority.Kept > 0
      || an.Priority.Free > 0
      || an.Surcharge.Gross != 0
      || an.Surcharge.Discounts != 0
      || an.WithdrawalFee.Income > 0
      || an.Promotional > 0
      || an.NetTransfers != 0
      || an.Duplicates.Count > 0;
  }

  // ---- stylesheet ---------------------------------------------------------

  private const string Css = """
@page { size: A4; margin: 14mm 14mm 12mm; }
* { box-sizing: border-box; }
body { font-family: -apple-system, "Segoe UI", Helvetica, Arial, sans-serif;
       color: #1a1a1a; font-size: 9.2pt; line-height: 1.45; margin: 0; }
.page { page-break-after: always; }
.page:last-child { page-break-after: auto; }
h1 { font-size: 20pt; letter-spacing: .22em; font-weight: 600; margin: 0; text-transform: uppercase; }
.brand { display: flex; justify-content: space-between; align-items: flex-start;
         border-bottom: 2px solid #1a1a1a; padding-bottom: 9px; margin-bottom: 20px; }
.brand .mark { font-size: 15pt; font-weight: 700; letter-spacing: .01em; }
.brand .tag { font-size: 6.6pt; letter-spacing: .32em; color: #666; margin-top: 3px; text-transform: uppercase; }
.brand .org { text-align: right; font-size: 7.6pt; color: #555; line-height: 1.6; }
.meta { display: flex; justify-content: space-between; gap: 26px; margin-bottom: 22px; }
.meta table { border-collapse: collapse; font-size: 8.6pt; }
.meta td { padding: 1.6px 0; }
.meta td:first-child { color: #777; padding-right: 16px; white-space: nowrap; }
.meta td:last-child { text-align: right; font-variant-numeric: tabular-nums; }
.blk { margin-bottom: 18px; }
.lbl { font-size: 6.8pt; letter-spacing: .3em; color: #888; text-transform: uppercase; margin-bottom: 5px; }
.who { font-size: 12pt; font-weight: 650; letter-spacing: .04em; }
.sub { color: #666; font-size: 8.4pt; }
table.line { width: 100%; border-collapse: collapse; margin-top: 8px; }
table.line th { text-align: left; font-size: 6.8pt; letter-spacing: .22em; color: #888;
                text-transform: uppercase; border-bottom: 1.2px solid #1a1a1a; padding: 0 0 5px; font-weight: 600; }
table.line th.r, table.line td.r { text-align: right; font-variant-numeric: tabular-nums; }
table.line td { padding: 11px 0; vertical-align: top; border-bottom: 1px solid #e6e6e6; }
table.line .desc b { font-weight: 620; }
table.line .desc p { margin: 4px 0 0; color: #555; font-size: 8.2pt; line-height: 1.5; }
.tot { width: 260px; margin-left: auto; margin-top: 12px; border-collapse: collapse; font-size: 8.8pt; }
.tot td { padding: 4px 0; }
.tot td.r { text-align: right; font-variant-numeric: tabular-nums; }
.tot tr.grand td { border-top: 1.6px solid #1a1a1a; padding-top: 9px; font-weight: 700; font-size: 10.4pt; }
h2 { font-size: 11.5pt; font-weight: 620; margin: 0 0 4px; }
.intro { color: #555; font-size: 8.4pt; margin-bottom: 16px; max-width: 92%; }
.cards { display: grid; grid-template-columns: 1fr 1fr; gap: 13px; margin-bottom: 15px; }
.card { border: 1px solid #dcdcdc; border-radius: 3px; padding: 10px 12px; }
.card .ch { font-size: 6.5pt; letter-spacing: .2em; color: #888; text-transform: uppercase;
            display: flex; justify-content: space-between; gap: 8px; margin-bottom: 7px; }
.card .ch span:last-child { letter-spacing: .06em; color: #aaa; }
.card table { width: 100%; border-collapse: collapse; font-size: 8pt; }
.card td { padding: 1.7px 0; }
.card td.r { text-align: right; font-variant-numeric: tabular-nums; }
.card tr.sum td { border-top: 1px solid #dcdcdc; padding-top: 4px; font-weight: 600; }
.card tr.rate td { font-weight: 650; }
.card .work { font-size: 7.2pt; color: #999; margin-top: 5px; font-variant-numeric: tabular-nums; }
.card .foot { font-size: 7.2pt; color: #777; margin-top: 5px; font-style: italic; }
table.grid { width: 100%; border-collapse: collapse; font-size: 8.4pt; }
table.grid th { font-size: 6.8pt; letter-spacing: .16em; text-transform: uppercase; color: #888;
                font-weight: 600; padding: 0 0 5px; border-bottom: 1.2px solid #1a1a1a; text-align: right; }
table.grid th:first-child { text-align: left; }
table.grid td { padding: 3.4px 0; text-align: right; font-variant-numeric: tabular-nums; border-bottom: 1px solid #f0f0f0; }
table.grid td:first-child { text-align: left; }
table.grid tr.sec td { font-size: 6.6pt; letter-spacing: .2em; text-transform: uppercase; color: #888;
                       padding: 11px 0 3px; border-bottom: none; font-weight: 600; }
table.grid tr.strong td { font-weight: 660; }
table.grid tr.rule td { border-top: 1.2px solid #1a1a1a; }
table.grid tr.final td { font-weight: 700; font-size: 9.4pt; border-top: 1.6px solid #1a1a1a; padding-top: 6px; }
table.grid .derived { color: #888; }
ol.basis { padding-left: 15px; margin: 4px 0 0; font-size: 7.9pt; color: #444; line-height: 1.55; }
ol.basis li { margin-bottom: 4.5px; }
.prov { display: grid; grid-template-columns: 1fr 1fr; gap: 13px; margin-top: 8px; }
.prov .box { border: 1px solid #dcdcdc; border-radius: 3px; padding: 9px 11px; }
.prov .bh { font-size: 6.5pt; letter-spacing: .18em; text-transform: uppercase; color: #888;
            margin-bottom: 6px; font-weight: 600; }
.prov ul { margin: 0; padding-left: 13px; font-size: 7.6pt; color: #444; line-height: 1.5; }
.prov li { margin-bottom: 3px; }
.pay { border-top: 1.2px solid #1a1a1a; margin-top: 18px; padding-top: 10px;
       display: flex; justify-content: space-between; align-items: flex-end; font-size: 8.2pt; }
.pay .r { text-align: right; color: #666; font-size: 7.8pt; }
.foot { margin-top: 16px; padding-top: 7px; border-top: 1px solid #e6e6e6;
        display: flex; justify-content: space-between; color: #999; font-size: 7pt; }
""";

  private const string Header = """
<div class="brand">
  <div><div class="mark">&#9638; BunnyBooker</div><div class="tag">KTMB Rail Booking</div></div>
  <div class="org">BUNNYBOOKER &middot; UEN 53478626W<br>60 Paya Lebar Road, #06-28<br>Paya Lebar Square, Singapore 409051<br>bunnybooker.com</div>
</div>
""";

  // ---- entry point --------------------------------------------------------

  public static string Render(InvoiceComputed c, InvoiceShare partner)
  {
    var sb = new StringBuilder(64 * 1024);
    sb.Append("<!doctype html><html><head><meta charset=\"utf-8\">\n<title>")
      .Append(Reference(c, partner))
      .Append(" &mdash; ")
      .Append(Esc(partner.Name))
      .Append("</title>\n<style>")
      .Append(Css)
      .Append("</style></head><body>\n");
    PageCover(sb, c, partner);
    PageAppendix(sb, c);
    PageTable(sb, c, partner);
    PageBasis(sb, c, partner);
    sb.Append("\n</body></html>");
    return sb.ToString();
  }

  // Every partner's document for the month, in the order the calculator
  // allocated the shares. Keyed by suffix because that is what the invoice
  // number carries and what the drift check compares on.
  public static IReadOnlyDictionary<string, string> RenderAll(InvoiceComputed c) =>
    c.Result.Shares.ToDictionary(p => p.Suffix, p => Render(c, p), StringComparer.Ordinal);

  // A stable, human-legible filename. The reference is already unique per
  // partner per month, so nothing else is needed to disambiguate.
  public static string FileName(InvoiceComputed c, InvoiceShare partner) =>
    $"{Reference(c, partner)}-{partner.Name.ToLowerInvariant().Replace(' ', '-')}.html";

  // ---- page 1: cover ------------------------------------------------------

  private static void PageCover(StringBuilder sb, InvoiceComputed c, InvoiceShare partner)
  {
    var t = c.Totals;
    var res = c.Result;
    var pct = Pct(partner.Pct);

    // The advance sentence is the only place the cover mentions money the
    // partner already holds. Omitted entirely when there is none, so June
    // reads exactly as it was issued.
    var advanceNote = c.Recovery.Applies
      ? $" Your {pct}% share is SGD {N2(partner.Earned)}; SGD {N2(partner.Advance)} of it was already collected directly from riders\n      outside BunnyBooker and is in your hands, so only the remainder is transferred."
      : "";

    var streams = HasAncillary(c)
      ? " (ticket sales, boost fees, withdrawal fees, terminated bookings)"
      : " (ticket sales, terminated bookings)";

    sb.Append($"""
<div class="page">
{Header}
<div class="meta">
  <div><h1>Invoice</h1></div>
  <table>
    <tr><td>Invoice no.</td><td>{Reference(c, partner)}</td></tr>
    <tr><td>Issue date</td><td>{Esc(c.Input.IssueDate)}</td></tr>
    <tr><td>Due date</td><td>{Esc(c.Input.DueDate)}</td></tr>
    <tr><td>Period</td><td>{Esc(c.Input.Period.Label)}</td></tr>
    <tr><td>Currency</td><td>SGD</td></tr>
  </table>
</div>
<div style="display:flex; gap:40px;">
  <div class="blk" style="flex:1">
    <div class="lbl">Billed to</div>
    <div class="who">{Esc(partner.Name)}</div>
    <div class="sub">Marketing partner<br>Attn: Accounts</div>
  </div>
  <div class="blk" style="flex:1">
    <div class="lbl">For</div>
    <div class="sub">Marketing profit-share<br>{Esc(c.Input.Period.MonthName)} booking operations<br>{pct}% of net profit (one of {c.Input.Partners.Length} equal shares)</div>
  </div>
</div>
<table class="line">
  <thead><tr><th>Description</th><th class="r">Amount (SGD)</th></tr></thead>
  <tbody><tr>
    <td class="desc"><b>Marketing partner profit-share &mdash; {Esc(c.Input.Period.MonthName)}</b>
      <p>{pct}% of net profit for the month. {N0(t.Tickets)} tickets delivered on SGD {N2(t.Revenue)} of ticket sales.
      Net profit SGD {N2(res.NetProfit)} &mdash; everything that came in{streams},
      minus everything that went out (KTMB fares, card processing fees, infrastructure). One total, itemised on the next page.{advanceNote}</p></td>
    <td class="r">{N2(partner.Earned)}</td>
  </tr></tbody>
</table>
<table class="tot">
  <tr><td>{(c.Recovery.Applies ? "Profit share earned" : "Subtotal")}</td><td class="r">{N2(partner.Earned)}</td></tr>
  {(c.Recovery.Applies ? $"""<tr><td>Less: already collected outside</td><td class="r">&minus;{N2(partner.Advance)}</td></tr>""" : "")}
  <tr><td>Tax (0%)</td><td class="r">0.00</td></tr>
  <tr class="grand"><td>Total due</td><td class="r">SGD {N2(partner.Amount)}</td></tr>
</table>
</div>
""");
  }

  // ---- page 2: appendix ---------------------------------------------------
  //
  // Four of the next page's lines use a blended rate rather than a raw figure.
  // This page is the working behind those four rates and adds nothing to the
  // total — a partner asking "where does 0.30263 come from" is answered here
  // rather than by us re-deriving it in a message six weeks later.

  private static void PageAppendix(StringBuilder sb, InvoiceComputed c)
  {
    var fx = c.Fx;
    var fee = c.Fee;
    var a = c.Adjustments;
    var mn = MonthWord(c);

    var topupRows = string.Concat(
      c.Input.Topups.Select(t =>
        $"""<tr><td>{Esc(t.Date)} &middot; top-up &larr; {N2(t.Rm)}</td><td class="r">RM &middot; {N2(t.Sgd)}</td></tr>"""
      )
    );

    var termRows = string.Concat(
      c.Routes.Select(rt =>
        $"""<tr><td>{Esc(rt.Short)} &middot; {rt.Terminated.Count} &middot; kept {N2(rt.Terminated.KeptRevenue)} &minus; &frac12; fare {N2(rt.Terminated.HalfFareSgd)}</td><td class="r">{N2(rt.TerminatedNet)}</td></tr>"""
      )
    );

    sb.Append($"""
<div class="page">
{Header}
<h2>Appendix &mdash; where the blended rates come from</h2>
<div class="intro">The next page is a single total: everything that came in, minus everything that went out.
Four of its lines use a blended rate rather than a raw figure &mdash; this page shows how each of those four
rates was derived from {Esc(mn)}'s actual records. Nothing here is added to the
total; it is working. All figures SGD unless marked RM.</div>
<div class="cards">
  <div class="card">
    <div class="ch"><span>FX rate &mdash; blended</span><span>SGD / RM</span></div>
    <table>{topupRows}
      <tr class="sum"><td>Total funded {N2(fx.TotalFundedRm)}</td><td class="r">RM &middot; {N2(fx.TotalFundedSgd)}</td></tr>
      <tr class="rate"><td>Blended rate</td><td class="r">{F(fx.FxRatePrinted, 5)} SGD/RM</td></tr>
    </table>
    <div class="work">{N2(fx.TotalFundedSgd)} &divide; {N2(fx.TotalFundedRm)} = {F(fx.FxRatePrinted, 5)}</div>
    {(string.IsNullOrEmpty(c.Input.TopupNote) ? "" : $"""<div class="foot">{Esc(c.Input.TopupNote)}</div>""")}
  </div>
  <div class="card">
    <div class="ch"><span>Processing fee &mdash; blended</span><span>% of deposits</span></div>
    <table>
      <tr><td>Gateway fees</td><td class="r">{N2(c.Input.Fees.Gateway)}</td></tr>
      <tr><td>Payment-method fees</td><td class="r">{N2(c.Input.Fees.PaymentMethod)}</td></tr>
      <tr class="sum"><td>Total payment fees</td><td class="r">{N2(fee.TotalPaymentFees)}</td></tr>
      <tr><td>Gross deposits ({Esc(mn)})</td><td class="r">{N2(c.Input.GrossDeposits)}</td></tr>
      <tr class="rate"><td>Blended rate</td><td class="r">{F(fee.FeeRatePct, 4)} %</td></tr>
    </table>
    <div class="work">{N2(fee.TotalPaymentFees)} &divide; {N2(c.Input.GrossDeposits)} = {F(fee.FeeRatePct, 4)}%</div>
  </div>
  <div class="card">
    <div class="ch"><span>Terminated &mdash; net</span><span>Assumes 50% KTMB refund</span></div>
    <table>{termRows}
      <tr class="sum"><td>{a.TerminatedCount} terminated bookings</td><td class="r">{N2(a.TerminatedNet)}</td></tr>
      <tr class="rate"><td>Net uplift</td><td class="r">+{N2(a.TerminatedNet)} SGD</td></tr>
    </table>
    <div class="foot">net = &frac12; revenue kept &minus; &frac12; KTMB fare</div>
  </div>
  <div class="card">
    <div class="ch"><span>Wasted fee &mdash; withdrawals</span><span>Amortized</span></div>
    <table>
      <tr><td>Paid out in {Esc(mn)} &middot; {c.Input.Withdrawals.Count} withdrawals</td><td class="r">{N2(c.Input.Withdrawals.Total)}</td></tr>
      <tr><td>&times; blended inbound fee {F(fee.FeeRatePct, 4)}%</td><td class="r">{N2(a.WastedFee)}</td></tr>
      <tr class="rate"><td>Added cost</td><td class="r">&minus;{N2(a.WastedFee)} SGD</td></tr>
    </table>
    <div class="foot">fee paid on money that left without a ticket</div>
  </div>
</div>
</div>
""");
  }

  // ---- page 3: the single total -------------------------------------------

  private static void PageTable(StringBuilder sb, InvoiceComputed c, InvoiceShare partner)
  {
    var routes = c.Routes;
    var t = c.Totals;
    var a = c.Adjustments;
    var res = c.Result;
    var an = c.Ancillary;
    var rec = c.Recovery;
    var pct = Pct(partner.Pct);
    var span = routes.Length + 2;

    string Cols(Func<InvoiceRouteResult, string> f) =>
      string.Concat(routes.Select(rt => $"<td>{f(rt)}</td>"));

    var dash = string.Concat(routes.Select(_ => "<td>&mdash;</td>"));

    // Every distinct surcharge/discount name seen in the month, in the order
    // the routes report them, so a line that exists on only one route still
    // prints.
    List<string> LineNames(InvoicePriceLineKind kind)
    {
      var seen = new List<string>();
      foreach (var rt in routes)
      foreach (var l in rt.PriceLines)
      {
        if (l.Kind == kind && !seen.Contains(l.Name, StringComparer.Ordinal))
          seen.Add(l.Name);
      }
      return seen;
    }

    decimal LineFor(InvoiceRouteResult rt, string name) =>
      rt.PriceLines.Where(l => l.Name == name).Sum(l => l.Delta);
    int LineCount(string name) =>
      routes.Sum(rt => rt.PriceLines.Where(l => l.Name == name).Sum(l => l.Count));
    decimal LineTotal(string name) => routes.Sum(rt => LineFor(rt, name));

    var policyNames = LineNames(InvoicePriceLineKind.Policy);
    var discountNames = LineNames(InvoicePriceLineKind.Discount);
    var hasPriceLines = policyNames.Count > 0 || discountNames.Count > 0;

    // Ticket revenue is built, not assumed: every booking starts at the SGD
    // 10.00 base fare, then late-booking surcharges push it up and discounts
    // pull it down. Both are INSIDE what the rider paid, so they build Revenue
    // here and are never added to it a second time further down.
    var revenueBuildUp = !hasPriceLines
      ? ""
      : $"""

    <tr class="derived"><td>&nbsp;&nbsp;Base fare &middot; {N0(t.Tickets)} tickets &times; SGD 10.00</td>{Cols(rt => N2(rt.Tickets * 10m))}<td>{N2(t.Tickets * 10m)}</td></tr>
    {string.Join("\n    ", policyNames.Select(nm => $"""<tr class="derived"><td>&nbsp;&nbsp;+ {Esc(nm)} &middot; {N0(LineCount(nm))} tickets</td>{Cols(rt => N2(LineFor(rt, nm)))}<td>{N2(LineTotal(nm))}</td></tr>"""))}
    {string.Join("\n    ", discountNames.Select(nm => $"""<tr class="derived"><td>&nbsp;&nbsp;&minus; {Esc(nm)} &middot; {N0(LineCount(nm))} tickets</td>{Cols(rt => N2(Math.Abs(LineFor(rt, nm))))}<td>{N2(Math.Abs(LineTotal(nm)))}</td></tr>"""))}
""";

    // ONE running total, two halves: everything that came IN, everything that
    // went OUT, and the difference. No intermediate subtotals (no
    // "contribution", no "ancillary net", no "adjustments") — those were three
    // competing totals on a page that only needs one, and they made it
    // impossible to tell whether the net profit already included them. It does:
    // every line below is in it, once.
    var moneyIn = R2(
      t.Revenue + an.Priority.Gross + an.Priority.Kept + an.WithdrawalFee.Income + a.TerminatedNet
    );
    var moneyOut = R2(
      t.TicketFaresSgd
        + t.ProcessingFee
        + an.Priority.FeeCost
        + a.WastedFee
        + an.Promotional
        + an.NetTransfers
        + a.Infrastructure
    );

    var inRows = $"""

    <tr><td>{(hasPriceLines ? "= " : "")}Ticket sales &middot; {N0(t.Tickets)} tickets{(hasPriceLines ? "" : " &times; blended fare")}</td>{Cols(rt => N2(rt.Revenue))}<td>{N2(t.Revenue)}</td></tr>
    {(HasAncillary(c) ? $"""<tr><td>Boost (priority) fees &middot; {N0(an.Priority.Paid)} &times; SGD 10.00{(an.Priority.Free > 0 ? $", {N0(an.Priority.Free)} given free" : "")}</td>{Cols(rt => N2(rt.Priority.Gross))}<td>{N2(an.Priority.Gross)}</td></tr>""" : "")}
    {(an.Priority.Kept > 0 ? $"""<tr><td>Boost fees kept on terminated bookings &middot; {N0(an.Priority.KeptCount)}</td>{dash}<td>{N2(an.Priority.Kept)}</td></tr>""" : "")}
    {(an.WithdrawalFee.Income > 0 ? $"""<tr><td>Withdrawal fees &middot; {N0(an.WithdrawalFee.WithFee)} of {N0(an.WithdrawalFee.Count)} payouts</td>{dash}<td>{N2(an.WithdrawalFee.Income)}</td></tr>""" : "")}
    <tr><td>Terminated bookings &middot; {N0(a.TerminatedCount)} kept, net of the KTMB refund</td>{Cols(rt => N2(rt.TerminatedNet))}<td>{N2(a.TerminatedNet)}</td></tr>
""";

    var outRows = $"""

    <tr><td>KTMB ticket fares &middot; RM {N2(t.TicketFaresRm)} @ {F(c.Fx.FxRatePrinted, 5)}</td>{Cols(rt => N2(rt.TicketFaresSgd))}<td>{N2(t.TicketFaresSgd)}</td></tr>
    <tr><td>Card processing fee on ticket sales &middot; {F(c.Fee.FeeRatePct, 4)}%</td>{Cols(rt => N2(rt.ProcessingFee))}<td>{N2(t.ProcessingFee)}</td></tr>
    {(HasAncillary(c) ? $"""<tr><td>Card processing fee on boost fees &middot; {F(c.Fee.FeeRatePct, 4)}%</td>{Cols(rt => N2(rt.Priority.FeeCost))}<td>{N2(an.Priority.FeeCost)}</td></tr>""" : "")}
    <tr><td>Card processing fee wasted on withdrawn funds</td>{dash}<td>{N2(a.WastedFee)}</td></tr>
    {(an.Promotional > 0 ? $"""<tr><td>Promotional credits granted to riders</td>{dash}<td>{N2(an.Promotional)}</td></tr>""" : "")}
    {(an.NetTransfers != 0 ? $"""<tr><td>Manual wallet adjustments, net{(an.NetTransfers < 0 ? " (received back)" : "")}</td>{dash}<td>{N2(an.NetTransfers)}</td></tr>""" : "")}
    <tr><td>Infrastructure &middot; flat monthly</td>{dash}<td>{N2(a.Infrastructure)}</td></tr>
""";

    // Money the partner collected OUTSIDE BunnyBooker. It never reached our
    // accounts, so it is deliberately NOT in Money in and does NOT touch net
    // profit — it is taken off the profit before the split, so the partner's
    // own half absorbs it. Sits between net profit and the share for exactly
    // that reason: it is the only thing standing between those two numbers.
    var recoveryRows = !rec.Applies
      ? ""
      : $"""

    <tr class="sec"><td colspan="{span}">Already collected outside BunnyBooker &mdash; paid in advance, so not paid again</td></tr>
    {(rec.FreeBoosts > 0 ? $"""<tr><td>&nbsp;&nbsp;Free boosts granted &middot; {N0(rec.FreeBoosts)} &times; {N2(rec.PerBoost)} collected from riders</td>{dash}<td>{N2(rec.Boosts)}</td></tr>""" : "")}
    {(rec.Tickets > 0 ? $"""<tr><td>&nbsp;&nbsp;Tickets bought on the partner account &middot; {N0(rec.Tickets)} &times; {N2(rec.PerTicket)} assumed resale margin</td>{dash}<td>{N2(rec.TicketsAmount)}</td></tr>""" : "")}
    <tr class="strong"><td>Total already in the partners' hands &mdash; {N2(partner.Advance)} of it this partner's</td>{dash}<td>{N2(rec.Total)}</td></tr>
""";

    // Per-ticket close-out. Everything here divides the ONE total above, so
    // there is nothing new to reconcile — it just restates it per ticket.
    // Guarded on the ticket count: a zero-ticket month is a legitimate
    // what-if in the preview, and it must not take the renderer down.
    var perTicketIn = t.Tickets == 0 ? 0m : R2(moneyIn / t.Tickets);
    var perTicketOut = t.Tickets == 0 ? 0m : R2(moneyOut / t.Tickets);
    var perTicketNet = t.Tickets == 0 ? 0m : R2(res.NetProfit / t.Tickets);
    var netMargin = moneyIn == 0 ? 0m : InvoiceRounding.R(res.NetProfit / moneyIn * 1000m, 0) / 10m;

    var perTicket = $"""

    <tr class="sec"><td colspan="{span}">Per ticket &mdash; all {N0(t.Tickets)} delivered</td></tr>
    <tr><td>Blended price per ticket (everything in &divide; tickets)</td>{dash}<td>{N2(perTicketIn)}</td></tr>
    <tr><td>Cost per ticket (everything out &divide; tickets)</td>{dash}<td>{N2(perTicketOut)}</td></tr>
    <tr class="strong"><td>Net profit per ticket</td>{dash}<td>{N2(perTicketNet)}</td></tr>
    {(an.Duplicates.Count > 0 ? $"""<tr class="derived"><td>Memo &middot; {N0(an.Duplicates.Count)} duplicate bookings refunded in full &mdash; no ticket bought, nets to zero</td>{dash}<td>{N2(an.Duplicates.Refunded)}</td></tr>""" : "")}

""";

    var payableRows = !rec.Applies
      ? ""
      : $"""

    <tr><td>Less: already collected outside BunnyBooker &mdash; this partner's half</td>{dash}<td>&minus;{N2(partner.Advance)}</td></tr>
    <tr class="final"><td>Payable to {Esc(partner.Name)} &mdash; share minus what is already held</td>{dash}<td>{N2(partner.Amount)}</td></tr>
""";

    sb.Append($"""
<div class="page">
{Header}
<table class="grid">
  <thead><tr><th>Line</th>{string.Concat(routes.Select(rt => $"<th>{Esc(rt.Label)}</th>"))}<th>Total</th></tr></thead>
  <tbody>
    <tr class="sec"><td colspan="{span}">Money in</td></tr>{revenueBuildUp}{inRows}
    <tr class="strong rule"><td>Total in</td>{dash}<td>{N2(moneyIn)}</td></tr>

    <tr class="sec"><td colspan="{span}">Money out</td></tr>{outRows}
    <tr class="strong rule"><td>Total out</td>{dash}<td>{N2(moneyOut)}</td></tr>

    <tr class="final"><td>Net profit &mdash; everything in, minus everything out</td>{dash}<td>{N2(res.NetProfit)}</td></tr>
    <tr class="derived"><td>Net margin (net profit &divide; everything in)</td>{dash}<td>{F(netMargin, 1)}%</td></tr>
{recoveryRows}
    <tr class="strong"><td>Marketing partner share &mdash; {pct}% of net profit</td>{dash}<td>{N2(partner.Earned)}</td></tr>
{payableRows}
{perTicket}  </tbody>
</table>
</div>
""");
  }

  // ---- page 4: basis, assumptions and provenance --------------------------
  //
  // This page is why the invoice is arguable. Every assumed figure names
  // itself as assumed and says where the real one lives.

  private static void PageBasis(StringBuilder sb, InvoiceComputed c, InvoiceShare partner)
  {
    var mn = MonthWord(c);
    var res = c.Result;
    var an = c.Ancillary;
    var pct = Pct(partner.Pct);
    var evenPct = Pct(res.Shares.Length > 0 ? res.Shares[0].Pct : 0m);
    var ancillary = HasAncillary(c);

    var boostLi = !ancillary
      ? ""
      : $"""
<li><b>Boost (priority) fees.</b> A flat SGD 10.00 queue-jump charge, billed as its own ledger entry and <i>not</i> folded into the
    booking price &mdash; which is why it appears as its own "money in" line rather than inside ticket sales. {N0(an.Priority.Paid)} were charged
    (SGD {N2(an.Priority.Gross)}); {N0(an.Priority.Free)} more were granted free and earn nothing. It arrives as a wallet deposit like
    any other payment, so the same {F(c.Fee.FeeRatePct, 4)}% processing fee applies &mdash; that fee is the separate "money out" line of
    SGD {N2(an.Priority.FeeCost)}, leaving SGD {N2(an.Priority.Net)} net.
    {(an.Priority.Kept > 0 ? $"A further SGD {N2(an.Priority.Kept)} was kept on {N0(an.Priority.KeptCount)} terminated bookings &mdash; the boost was already spent, so the fee is not refunded. Cancelled and refunded bookings <i>do</i> get it back and earn nothing. No route split exists for these (they never completed), so they sit at the total level." : "")}</li>
  {(an.WithdrawalFee.Income > 0 ? $"<li><b>Withdrawal fees.</b> SGD {N2(an.WithdrawalFee.Income)} kept across {N0(an.WithdrawalFee.WithFee)} of {N0(an.WithdrawalFee.Count)} payouts (the fee shipped 7 July 2026, so earlier payouts carry none). Collected on the way out, so it bears no inbound gateway fee &mdash; distinct from the wasted-fee line above, which is a cost on the same money.</li>" : "")}
  {(an.Promotional > 0 || an.NetTransfers != 0 ? $"""
<li><b>Giveaways and manual corrections.</b>
    {(an.Promotional > 0 ? $"SGD {N2(an.Promotional)} of promotional credits handed to riders as goodwill &mdash; a real giveaway, so it is money out. " : "")}
    {(an.NetTransfers != 0 ? $"Net SGD {N2(Math.Abs(an.NetTransfers))} {(an.NetTransfers > 0 ? "paid out by BunnyBooker" : "received by BunnyBooker")} from hand-made corrections between the operating account and rider wallets." : "")}</li>
""" : "")}
  <li><b>Surcharges and discounts are already inside ticket sales.</b> A late-booking surcharge raises the fare the rider pays and a discount lowers it,
    so neither is separate income or a separate cost &mdash; they are shown indented under ticket sales as working, never as their own line in the total.
    The base fare is <b>SGD 10.00 on every booking</b>, so it reconciles to the cent: {N0(c.Totals.Tickets)} tickets &times; 10.00 = {N2(c.Totals.Tickets * 10m)},
    plus {N2(an.Surcharge.Gross)} of surcharges, less {N2(Math.Abs(an.Surcharge.Discounts))} of discounts, gives exactly the
    SGD {N2(c.Totals.Revenue)} shown. Figures are derived from what each rider actually paid, so all {N0(c.Totals.Tickets)} tickets are covered &mdash;
    not from the stored price breakdown, which only began mid-July 2026 and would leave part of the month unmeasured.</li>
  {(an.Duplicates.Count > 0 ? $"<li>Duplicate bookings ({N0(an.Duplicates.Count)}, SGD {N2(an.Duplicates.Refunded)} refunded) net to zero and are excluded from profit. Every one has no recorded KTMB cost &mdash; no ticket was ever bought &mdash; so refunding the rider costs nothing beyond the processing fee already accounted for. Listed for completeness.</li>" : "")}
""";

    var recoveryLi = !c.Recovery.Applies
      ? ""
      : $"<li>Already collected outside BunnyBooker &mdash; an advance, not a cost. {Esc(mn)} had {N0(c.Recovery.FreeBoosts)} boosts granted at no charge on the partner account and {N0(c.Recovery.Tickets)} tickets bought on it. That money was collected from riders directly and never entered a BunnyBooker account, so it changes none of the figures above: the net profit is SGD {N2(res.NetProfit)} either way and the {evenPct}% share is still SGD {N2(partner.Earned)}. What it changes is the <em>transfer</em>. SGD {N2(c.Recovery.Total)} ({N0(c.Recovery.FreeBoosts)} &times; {N2(c.Recovery.PerBoost)} plus {N0(c.Recovery.Tickets)} &times; {N2(c.Recovery.PerTicket)}) is already in the partners' hands, SGD {N2(partner.Advance)} of it this partner's, so that much is treated as paid and only SGD {N2(partner.Amount)} is transferred.</li>";

    var blendedPerTicket =
      c.Totals.Tickets == 0
        ? 0m
        : R2(
          (
            c.Totals.Revenue
            + an.Priority.Gross
            + an.Priority.Kept
            + an.WithdrawalFee.Income
            + c.Adjustments.TerminatedNet
          ) / c.Totals.Tickets
        );

    var ancillaryProvenance = !ancillary
      ? ""
      : $"""
<li>Priority fees {N2(an.Priority.Gross)} ({N0(an.Priority.Paid)} charged, {N0(an.Priority.Free)} free) &mdash; zinc ledger, PriorityFee entries</li>
      {(an.WithdrawalFee.Income > 0 ? $"<li>Withdrawal fees {N2(an.WithdrawalFee.Income)} &mdash; zinc withdrawals DB, Fee column</li>" : "")}
      {(an.Promotional > 0 ? $"<li>Promotional credits {N2(an.Promotional)} &amp; transfers &mdash; zinc ledger</li>" : "")}
      <li>Surcharges {N2(an.Surcharge.Gross)} / discounts {N2(an.Surcharge.Discounts)} &mdash; derived from prices actually paid vs the 10.00 base (all {N0(c.Totals.Tickets)} tickets)</li>
""";

    var ancillaryDerived = !ancillary
      ? ""
      : $"""
<li>Boost processing fee {N2(an.Priority.FeeCost)} &mdash; derived (boost gross &times; blended fee)</li>
      <li>Base fare 10.00/ticket &mdash; authoritative (BaseCost on every stored breakdown); surcharge = paid above it, discount = below</li>
""";

    sb.Append($"""
<div class="page">
{Header}
<h2>Basis &amp; assumptions</h2>
<ol class="basis">
  <li><b>One total, two halves.</b> The invoice adds up everything that came in and everything that went out; net profit is the difference, and the partner
    share is {pct}% of that. There are no sub-totals to reconcile &mdash; every line on the page is already inside net profit exactly once.</li>
  <li>Ticket sales = completed bookings only (tickets delivered), by completion date in {Esc(mn)}, Singapore time. Refunded/cancelled bookings excluded.</li>
  <li>Price per ticket is derived, not a list price &mdash; a blended average. Actual charges vary by rider: standard fares alongside custom friends-&amp;-family, beta-tester and bug-tester rates.</li>
  <li>FX &mdash; blended actual. {F(c.Fx.FxRatePrinted, 5)} SGD/RM = total SGD paid to top up the KTMB card &divide; RM received, across all {Esc(mn)} top-ups.{(string.IsNullOrEmpty(c.Input.TopupNote) ? "" : " " + Esc(c.Input.TopupNote))}</li>
  <li>Processing fee &mdash; blended actual. {F(c.Fee.FeeRatePct, 4)}% = {Esc(mn)} Airwallex payment fees &divide; gross deposits, applied to each route's revenue. Refund fees (SGD {N2(c.Input.RefundFeesExcluded)}) excluded &mdash; they relate to refunded bookings, not sold tickets.</li>
  <li>Infrastructure is a flat SGD {N2(c.Input.Infrastructure)}/month shared cost, not attributable to a single route, so it is deducted once at the total level.</li>
  <li>Deposits &ne; revenue. SGD {N2(c.Input.GrossDeposits)} was collected in deposits; the excess over revenue is unspent rider wallet balance (a liability) and is excluded.</li>
  <li>Terminated bookings ({c.Adjustments.TerminatedCount}). Ticket was bought then cancelled: rider is refunded 50% (you keep 50%) and KTMB refunds 50% of the fare. Net = &frac12; revenue kept &minus; &frac12; fare = +SGD {N2(c.Adjustments.TerminatedNet)}. The 50% KTMB refund is assumed &mdash; the actual amount is not stored in our system (only in the KTMB eWallet); confirm there for an exact figure.</li>
  <li>Wasted processing fee. SGD {N2(c.Input.Withdrawals.Total)} was withdrawn by riders in {Esc(mn)}; the inbound Airwallex fee on that money ({F(c.Fee.FeeRatePct, 4)}%) earned no ticket revenue, so SGD {N2(c.Adjustments.WastedFee)} is booked as cost.</li>
  {boostLi}
  <li>Profit-share split. The agreed {Pct(c.Input.MarketingSharePct)}% marketing share (SGD {N2(res.MarketingSharePool)}) is divided equally between {c.Input.Partners.Length} partners at {evenPct}% each. This invoice bills one {pct}% share; the net profit and all workings are identical for both.</li>
  {recoveryLi}
</ol>
<div class="lbl" style="margin-top:18px">Data provenance &mdash; authoritative vs derived</div>
<div class="prov">
  <div class="box">
    <div class="bh">Authoritative &mdash; from records</div>
    <ul>
      <li>Completed revenue {N2(c.Totals.Revenue)} &amp; ticket counts &mdash; zinc bookings DB</li>
      <li>Terminated {string.Join(" / ", c.Routes.Select(r => r.Terminated.Count.ToString(CultureInfo.InvariantCulture)))} &amp; kept revenue {string.Join(" / ", c.Routes.Select(r => N2(r.Terminated.KeptRevenue)))} &mdash; zinc DB</li>
      <li>Rider refund 50% &mdash; zinc config (RefundPercentage = 50)</li>
      <li>Withdrawn {N2(c.Input.Withdrawals.Total)} ({c.Input.Withdrawals.Count} payouts) &mdash; zinc withdrawals DB</li>
      <li>Airwallex fees {N2(c.Fee.TotalPaymentFees)} &amp; deposits {N2(c.Input.GrossDeposits)} &mdash; settlement export</li>
      <li>Top-up SGD / RM amounts &mdash; Airwallex card statement</li>
      <li>KTMB fares {string.Join(" / ", c.Routes.Select(r => N2(r.FareRm)))} RM &amp; infrastructure SGD {N2(c.Input.Infrastructure)} &mdash; provided</li>
      {ancillaryProvenance}
    </ul>
  </div>
  <div class="box">
    <div class="bh">Derived / assumed</div>
    <ul>
      <li>FX {F(c.Fx.FxRatePrinted, 5)} SGD/RM &mdash; derived (SGD &divide; RM topped up)</li>
      <li>Processing fee {F(c.Fee.FeeRatePct, 4)}% &mdash; derived (fees &divide; deposits)</li>
      <li>Ticket cost SGD &mdash; derived (RM fare &times; FX)</li>
      <li>Blended price per ticket {N2(blendedPerTicket)} &mdash; derived (everything in &divide; tickets)</li>
      <li>KTMB refund 50% of fare &mdash; assumed (not stored; confirm in KTMB eWallet)</li>
      <li>Terminated net +{N2(c.Adjustments.TerminatedNet)} &mdash; derived from the above</li>
      <li>Wasted fee {N2(c.Adjustments.WastedFee)} &mdash; derived (withdrawn &times; blended fee, amortized)</li>
      {ancillaryDerived}
    </ul>
  </div>
</div>
<div class="pay">
  <div><div class="lbl">Payment</div>Due within 14 days of issue.<br>Payment details provided separately.</div>
  <div class="r">Reference: {Reference(c, partner)}<br>Thank you.</div>
</div>
<div class="foot"><span>BunnyBooker &middot; Profit-share invoice</span><span>Sources: zinc bookings &middot; Airwallex settlement &amp; card statement, {Esc(c.Input.Period.MonthName)}</span></div>
</div>
""");
  }
}
