using Domain.Invoice;
using FluentAssertions;
using FluentAssertions.Execution;

namespace UnitTest.Invoices;

// The gate on the backfill.
//
// June, July and August are being written into the system as issued invoices,
// carrying figures this engine did not originally produce. The inputs for them
// are hand-copied, and a copy error would not announce itself: the engine would
// compute a complete, internally consistent invoice from the wrong input and
// store it as ISSUED — the strongest claim this system makes about money, on a
// month nobody will ever re-read.
//
// So the tests below are about one question: does a wrong transcription get
// REFUSED. The three real months are used as the honest case, and each of the
// ways a transcription can go wrong is checked against them.
public class InvoiceAttestationTests
{
  // What the three issued documents actually say. Transcribed from the PDFs,
  // the same source as InvoiceCalculatorGoldenFixtureTests.
  private static InvoiceAttestation June =>
    new()
    {
      Tickets = 2713,
      Revenue = 26933m,
      NetProfit = 16542.34m,
      Amounts = new Dictionary<string, decimal> { ["C"] = 4135.58m, ["Z"] = 4135.59m },
    };

  private static InvoiceAttestation July =>
    new()
    {
      Tickets = 4777,
      Revenue = 47765m,
      NetProfit = 32023.46m,
      Amounts = new Dictionary<string, decimal> { ["C"] = 7767.36m, ["Z"] = 7767.37m },
    };

  private static InvoiceAttestation August =>
    new()
    {
      Tickets = 5265,
      Revenue = 53219m,
      NetProfit = 37471.51m,
      Amounts = new Dictionary<string, decimal> { ["C"] = 9159.38m, ["Z"] = 9159.38m },
    };

  private static InvoiceComputed Computed(string month) =>
    InvoiceCalculator.Compute(InvoiceFixture.Input(month));

  [Theory]
  [InlineData("2026-06")]
  [InlineData("2026-07")]
  [InlineData("2026-08")]
  public void The_three_issued_months_transcribe_without_complaint(string month)
  {
    var attested = month switch
    {
      "2026-06" => June,
      "2026-07" => July,
      _ => August,
    };

    InvoiceAttestationCheck.Compare(attested, Computed(month)).Should().BeEmpty();
  }

  // A cent. This is the resolution the check has to have, because a cent is
  // the resolution the disagreements in this project have actually had — the
  // June wasted-fee variance was one.
  [Fact]
  public void A_single_cent_of_disagreement_is_reported()
  {
    var wrong = July with { NetProfit = 32023.47m };

    var m = InvoiceAttestationCheck.Compare(wrong, Computed("2026-07"));

    using var _ = new AssertionScope();
    m.Should().HaveCount(1);
    m[0].Field.Should().Be("netProfit");
    m[0].Attested.Should().Be(32023.47m);
    m[0].Computed.Should().Be(32023.46m);
  }

  [Fact]
  public void A_mistyped_ticket_count_is_reported()
  {
    // 5,625 for 5,265 — a transposition, and the shape of error that a
    // human copying figures actually makes.
    var wrong = August with { Tickets = 5625 };

    var m = InvoiceAttestationCheck.Compare(wrong, Computed("2026-08"));

    m.Should().ContainSingle(x => x.Field == "tickets" && x.Attested == 5625m);
  }

  // The one that matters most: what a partner was paid.
  [Fact]
  public void A_wrong_partner_amount_is_reported_against_that_partner()
  {
    var wrong = July with
    {
      Amounts = new Dictionary<string, decimal> { ["C"] = 7767.36m, ["Z"] = 7676.37m },
    };

    var m = InvoiceAttestationCheck.Compare(wrong, Computed("2026-07"));

    using var _ = new AssertionScope();
    m.Should().HaveCount(1);
    m[0].Field.Should().Be("shares.Z.amount");
    m[0].Attested.Should().Be(7676.37m);
    m[0].Computed.Should().Be(7767.37m);
  }

  // A partner who was paid but is missing from the attestation. This is the
  // error a total cannot catch — the remaining partners still sum to the pool,
  // so every figure the operator DID copy is correct.
  [Fact]
  public void A_partner_left_out_of_the_attestation_is_reported()
  {
    var wrong = August with
    {
      Amounts = new Dictionary<string, decimal> { ["C"] = 9159.38m },
    };

    var m = InvoiceAttestationCheck.Compare(wrong, Computed("2026-08"));

    using var _ = new AssertionScope();
    m.Should().HaveCount(1);
    m[0].Field.Should().Be("shares.Z.amount");
    m[0].Attested.Should().Be(0m);
    m[0].Computed.Should().Be(9159.38m);
  }

  // The mirror: a partner the operator says was paid, whom the inputs do not
  // include at all. Means the partner list in the transcribed inputs is wrong.
  [Fact]
  public void A_partner_the_inputs_do_not_know_about_is_reported()
  {
    var wrong = June with
    {
      Amounts = new Dictionary<string, decimal>
      {
        ["C"] = 4135.58m,
        ["Z"] = 4135.59m,
        ["X"] = 100m,
      },
    };

    var m = InvoiceAttestationCheck.Compare(wrong, Computed("2026-06"));

    using var _ = new AssertionScope();
    m.Should().HaveCount(1);
    m[0].Field.Should().Be("shares.X.amount");
    m[0].Attested.Should().Be(100m);
    m[0].Computed.Should().Be(0m);
  }

  // A suffix is one letter a human types into a form.
  [Fact]
  public void Partner_suffixes_match_regardless_of_case()
  {
    var lower = July with
    {
      Amounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
      {
        ["c"] = 7767.36m,
        ["z"] = 7767.37m,
      },
    };

    InvoiceAttestationCheck.Compare(lower, Computed("2026-07")).Should().BeEmpty();
  }

  // Every wrong figure at once, not just the first. An operator who has to
  // re-open the PDF should find out everything to check while it is open.
  [Fact]
  public void Every_disagreement_is_reported_not_just_the_first()
  {
    var wrong = August with
    {
      Tickets = 5264,
      Revenue = 53218m,
      NetProfit = 37471.50m,
      Amounts = new Dictionary<string, decimal> { ["C"] = 9159.37m, ["Z"] = 9159.38m },
    };

    var m = InvoiceAttestationCheck.Compare(wrong, Computed("2026-08"));

    m.Select(x => x.Field)
      .Should()
      .BeEquivalentTo(["tickets", "revenue", "netProfit", "shares.C.amount"]);
  }
}
