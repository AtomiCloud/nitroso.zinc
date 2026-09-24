using Domain.Invoice;
using FluentAssertions;

namespace UnitTest.Invoices;

// The drift check is what stops freezing from hiding an engine bug.
//
// Once an invoice is issued it renders from stored figures and never calls
// the calculator, which means a later correction is invisible on exactly the
// documents most worth checking. These tests pin the two things that makes
// useful: it must be SILENT when nothing changed (or it becomes noise nobody
// reads), and it must NOT be silent when a partner's payable moved.
public class InvoiceDriftCheckTests
{
  private static InvoiceFigures FiguresFor(string month) =>
    InvoiceFigures.Of(InvoiceCalculator.Compute(InvoiceFixture.Input(month)));

  [Theory]
  [InlineData("2026-06")]
  [InlineData("2026-07")]
  [InlineData("2026-08")]
  public void An_unchanged_engine_reports_no_drift_on_the_real_issued_months(string month)
  {
    // The baseline claim. If this ever fails, either the engine changed
    // without a version bump or the check is comparing something
    // non-deterministic — and both make every other drift report worthless.
    var inputs = InvoiceFixture.Input(month);
    var drift = InvoiceDriftCheck.Compare(
      Guid.NewGuid(),
      InvoiceEngine.Version,
      FiguresFor(month),
      inputs
    );

    drift.HasDrift.Should().BeFalse();
    drift.Fields.Should().BeEmpty();
    drift.FrozenEngineVersion.Should().Be(drift.CurrentEngineVersion);
  }

  [Fact]
  public void A_changed_payable_is_reported_against_the_partner_it_belongs_to()
  {
    // The frozen figures say a partner was paid a cent more than today's
    // engine computes. That is the whole point of the check.
    var inputs = InvoiceFixture.Input("2026-07");
    var frozen = FiguresFor("2026-07");
    var tampered = frozen with
    {
      Shares = frozen.Shares.ToDictionary(
        kv => kv.Key,
        kv => kv.Key == "C" ? kv.Value with { Amount = kv.Value.Amount + 0.01m } : kv.Value,
        StringComparer.OrdinalIgnoreCase
      ),
    };

    var drift = InvoiceDriftCheck.Compare(Guid.NewGuid(), 1, tampered, inputs);

    drift.HasDrift.Should().BeTrue();
    var field = drift.Fields.Should().ContainSingle().Subject;
    field.Path.Should().Be("shares.C.amount");
    field.Delta.Should().Be(-0.01m);
  }

  [Fact]
  public void The_reported_delta_points_from_frozen_to_current()
  {
    // Sign convention, pinned because a flipped sign turns "we underpaid" into
    // "we overpaid" on a screen someone acts on.
    var inputs = InvoiceFixture.Input("2026-07");
    var frozen = FiguresFor("2026-07") with { NetProfit = 30_000m };

    var field = InvoiceDriftCheck
      .Compare(Guid.NewGuid(), 1, frozen, inputs)
      .Fields.Should()
      .ContainSingle(f => f.Path == "result.netProfit")
      .Subject;

    field.Frozen.Should().Be(30_000m);
    field.Current.Should().Be(32_023.46m);
    field.Delta.Should().Be(field.Current - field.Frozen);
  }

  [Fact]
  public void A_partner_who_disappeared_is_reported_as_going_to_zero()
  {
    // Not silence. If the settings change removed a partner, the frozen
    // invoice still paid them and someone has to see that.
    var inputs = InvoiceFixture.Input("2026-07");
    var frozen = FiguresFor("2026-07");
    var withGhost = frozen with
    {
      Shares = new Dictionary<string, InvoicePartnerFigures>(
        frozen.Shares,
        StringComparer.OrdinalIgnoreCase
      )
      {
        ["X"] = new() { Amount = 500m, Advance = 0m },
      },
    };

    var drift = InvoiceDriftCheck.Compare(Guid.NewGuid(), 1, withGhost, inputs);

    var field = drift.Fields.Should().ContainSingle(f => f.Path == "shares.X.amount").Subject;
    field.Frozen.Should().Be(500m);
    field.Current.Should().Be(0m);
  }

  [Fact]
  public void Reordering_the_partners_is_not_drift()
  {
    // Shares are compared by suffix, not by position. Comparing positionally
    // would report every partner's amount as changed whenever the settings
    // list is reordered — noise that would train the operator to ignore the
    // warning.
    var inputs = InvoiceFixture.Input("2026-07");
    var reversed = inputs with { Partners = inputs.Partners.Reverse().ToArray() };
    var frozen = InvoiceFigures.Of(InvoiceCalculator.Compute(inputs));

    var drift = InvoiceDriftCheck.Compare(Guid.NewGuid(), 1, frozen, reversed);

    // July's pool splits to a half cent, and the leftover cent is allocated by
    // rounding preference rather than by list order, so reversing the list
    // must not move a single figure.
    drift.HasDrift.Should().BeFalse();
  }

  [Fact]
  public void An_engine_version_gap_alone_is_not_drift()
  {
    // A version bump that did not change any figure on THIS invoice must not
    // raise a warning about it. The versions are reported either way so a
    // human can see the gap; only differing money counts as drift.
    var inputs = InvoiceFixture.Input("2026-08");
    var drift = InvoiceDriftCheck.Compare(Guid.NewGuid(), 0, FiguresFor("2026-08"), inputs);

    drift.HasDrift.Should().BeFalse();
    drift.FrozenEngineVersion.Should().Be(0);
    drift.CurrentEngineVersion.Should().Be(InvoiceEngine.Version);
  }

  [Fact]
  public void The_figures_projection_carries_the_payable_of_every_partner()
  {
    // InvoiceFigures is the entire comparison surface: anything it drops can
    // never be reported as drift. This pins that the per-partner payable —
    // the only number anyone is actually paid — is in it for every partner.
    var c = InvoiceCalculator.Compute(InvoiceFixture.Input("2026-08"));
    var figures = InvoiceFigures.Of(c);

    figures.Shares.Should().HaveCount(c.Result.Shares.Length);
    foreach (var share in c.Result.Shares)
    {
      figures.Shares[share.Suffix].Amount.Should().Be(share.Amount);
      figures.Shares[share.Suffix].Advance.Should().Be(share.Advance);
    }

    // and the headline figures the operator reads off the list page
    figures.NetProfit.Should().Be(37_471.51m);
    figures.Tickets.Should().Be(5_265);
  }
}
