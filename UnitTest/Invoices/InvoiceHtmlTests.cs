using System.Text.RegularExpressions;
using App.Modules.Invoices.API.V1;
using Domain.Invoice;
using FluentAssertions;
using FluentAssertions.Execution;

namespace UnitTest.Invoices;

// The document differential gate.
//
// Six HTML files under Fixtures/render-*.html are the output of
// invoices/render.ts for June, July and August 2026 — the same renderer that
// produced the PDFs actually sent to the partners. This asserts that the C#
// renderer reproduces them.
//
// WHY THIS MATTERS MORE THAN IT LOOKS. The calculator is already pinned to the
// cent, but a partner never sees the calculator: they see this document. A
// transposed pair in the template, a figure formatted to the wrong number of
// decimals, or a conditional row that silently stops rendering would produce a
// perfectly plausible invoice carrying the wrong money, and no calculator test
// would notice. Three real months, two partners each, is the cheapest
// available proof that the port did not change what the partner reads.
//
// COMPARISON IS WHITESPACE-INSENSITIVE, and that is a deliberate weakening.
// The two engines interpolate identical markup but differ in where template
// literals leave newlines and indentation — differences HTML does not render
// and a reader cannot see. Comparing raw bytes would fail on all six files for
// reasons that have nothing to do with the invoice. What is NOT relaxed: every
// tag, attribute, number and word must match exactly, so a changed figure or a
// dropped row still fails.
public class InvoiceHtmlTests
{
  private static string Path(string name) =>
    System.IO.Path.Combine(AppContext.BaseDirectory, "Invoices", "Fixtures", name);

  // Collapses runs of whitespace, then drops whitespace adjacent to a tag
  // boundary. Anything a browser would treat as identical, this treats as
  // identical. Text content keeps its single separating spaces, so a changed
  // figure or a dropped word still fails.
  private static string Normalize(string html)
  {
    var collapsed = Regex.Replace(html, @"\s+", " ");
    return Regex.Replace(Regex.Replace(collapsed, @">\s+", ">"), @"\s+<", "<").Trim();
  }

  [Theory]
  [InlineData("2026-06", "CLEON", "cleon")]
  [InlineData("2026-06", "ZOEY", "zoey")]
  [InlineData("2026-07", "CLEON", "cleon")]
  [InlineData("2026-07", "ZOEY", "zoey")]
  [InlineData("2026-08", "CLEON", "cleon")]
  [InlineData("2026-08", "ZOEY", "zoey")]
  public void The_document_reproduces_what_the_typescript_renderer_produced(
    string month,
    string partner,
    string slug
  )
  {
    var computed = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    var share = computed.Result.Shares.Single(s => s.Name == partner);

    var actual = InvoiceHtml.Render(computed, share);
    var expected = File.ReadAllText(Path($"render-{month}-{slug}.html"));

    Normalize(actual).Should().Be(Normalize(expected));
  }

  // June predates boost fees, withdrawal fees, surcharges and every other
  // ancillary stream. render.ts omits that whole section rather than printing
  // a wall of zeros, so a regenerated June stays identical to the document on
  // file. If this ever inverts, June's issued invoices stop matching their
  // reproductions.
  [Fact]
  public void June_omits_the_ancillary_section_entirely()
  {
    var computed = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-06"));
    var html = InvoiceHtml.Render(computed, computed.Result.Shares[0]);

    using var _ = new AssertionScope();
    html.Should().NotContain("Boost (priority) fees");
    html.Should().NotContain("Withdrawal fees &middot;");
    html.Should().NotContain("Already collected outside BunnyBooker");
  }

  // July and August do carry a recovery, and it is the one thing standing
  // between the partner's share and what actually gets transferred. If the
  // section vanished, the document would bill the full share.
  [Theory]
  [InlineData("2026-07", "7,767.36")]
  [InlineData("2026-08", "9,159.38")]
  public void The_payable_a_partner_reads_is_the_amount_after_the_advance(
    string month,
    string payable
  )
  {
    var computed = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    var cleon = computed.Result.Shares.Single(s => s.Name == "CLEON");
    var html = InvoiceHtml.Render(computed, cleon);

    using var _ = new AssertionScope();
    html.Should().Contain("Already collected outside BunnyBooker");
    html.Should().Contain($"Total due</td><td class=\"r\">SGD {payable}");
  }

  // The invoice number is the partner's reference for the payment. render.ts
  // hardcodes "BB-2026-", which is right for every month issued so far and
  // silently wrong from January onward — and this endpoint exists so invoices
  // can be generated in later years. The year is derived from the period
  // instead; these pin that it still reads 2026 for the issued months.
  [Theory]
  [InlineData("2026-06", "BB-2026-0601-C")]
  [InlineData("2026-07", "BB-2026-0701-C")]
  [InlineData("2026-08", "BB-2026-0801-C")]
  public void The_reference_carries_the_year_the_period_belongs_to(string month, string reference)
  {
    var computed = InvoiceCalculator.Compute(InvoiceFixture.Input(month));
    var cleon = computed.Result.Shares.Single(s => s.Name == "CLEON");

    InvoiceHtml.Render(computed, cleon).Should().Contain(reference);
  }

  [Fact]
  public void A_later_year_gets_that_year_in_its_reference()
  {
    var input = InvoiceFixture.Input("2026-08") with
    {
      Period = new InvoicePeriod
      {
        Label = "1 - 31 January 2027",
        MonthName = "January 2027",
        Seq = "01",
      },
    };

    InvoiceHtml
      .Render(InvoiceCalculator.Compute(input), InvoiceCalculator.Compute(input).Result.Shares[0])
      .Should()
      .Contain("BB-2027-01-");
  }

  // Every figure on the page is escaped through the same helper, but the
  // fields that come from an operator rather than from the database are the
  // ones an injection would arrive through. A partner name is free text on the
  // settings page.
  [Fact]
  public void Operator_supplied_text_cannot_inject_markup()
  {
    var input = InvoiceFixture.Input("2026-08");
    var input2 = input with
    {
      Partners =
      [
        input.Partners[0] with { Name = "<script>alert(1)</script>" },
        input.Partners[1],
      ],
      TopupNote = "note & <b>bold</b>",
    };

    var computed = InvoiceCalculator.Compute(input2);
    var html = InvoiceHtml.Render(computed, computed.Result.Shares[0]);

    using var _ = new AssertionScope();
    html.Should().NotContain("<script>");
    html.Should().Contain("&lt;script&gt;");
    html.Should().Contain("note &amp; &lt;b&gt;bold&lt;/b&gt;");
  }

  // A preview is allowed to be a what-if, including one with no tickets in it
  // at all. The per-ticket close-out divides by the ticket count, so an
  // unguarded renderer would throw while the operator was merely exploring —
  // and push them back to the spreadsheet this whole build exists to retire.
  [Fact]
  public void A_month_with_no_tickets_renders_rather_than_dividing_by_zero()
  {
    var input = InvoiceFixture.Input("2026-08");
    var empty = input with
    {
      Routes = input
        .Routes.Select(r => r with
        {
          Tickets = 0,
          Revenue = 0m,
          Terminated = new InvoiceTerminatedInput
          {
            Count = 0,
            KeptRevenue = 0m,
            HalfFareSgd = 0m,
          },
        })
        .ToArray(),
      Priority = new InvoicePriorityInput
      {
        PerRoute = input.Priority.PerRoute.ToDictionary(
          kv => kv.Key,
          kv => new InvoicePriorityRouteInput
          {
            Paid = 0,
            Fee = 0m,
            Free = 0,
          }
        ),
        KeptOnCancelled = 0m,
        KeptOnCancelledCount = 0,
      },
      WithdrawalFee = new InvoiceWithdrawalFeeInput
      {
        Income = 0m,
        WithFee = 0,
        Count = 0,
      },
    };

    var computed = InvoiceCalculator.Compute(empty);
    var render = () => InvoiceHtml.Render(computed, computed.Result.Shares[0]);

    render.Should().NotThrow();
  }

  [Fact]
  public void Every_partner_gets_their_own_document()
  {
    var computed = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-08"));
    var all = InvoiceHtml.RenderAll(computed);

    using var _ = new AssertionScope();
    all.Keys.Should().BeEquivalentTo(["C", "Z"]);
    all["C"].Should().Contain("CLEON").And.NotContain("Billed to</div>\n    <div class=\"who\">ZOEY");
    all["C"].Should().NotBe(all["Z"]);
  }

  [Fact]
  public void The_filename_identifies_the_month_and_the_partner()
  {
    var computed = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-08"));
    var cleon = computed.Result.Shares.Single(s => s.Name == "CLEON");

    InvoiceHtml.FileName(computed, cleon).Should().Be("BB-2026-0801-C-cleon.html");
  }
}
