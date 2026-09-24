using Domain.Invoice;
using FluentAssertions;

namespace UnitTest.Invoices;

// Effective-dating for the invoice's agreed terms. The rules that carry money
// risk and are pinned here:
//
//   * an invoice asks for the terms at ITS OWN period, not today's, so a
//     later change must never reach back into a past month;
//   * partners resolve per suffix, so changing one partner does not require
//     re-entering the other; and
//   * "not configured" is null, never zero — a 0% share would pay the
//     partners nothing and still look like a valid invoice.
public class InvoiceSettingsScheduleTests
{
  private static readonly DateTime Jan = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime Jun = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
  private static readonly DateTime Sep = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

  private static InvoiceSettingsChange Settings(
    decimal pct,
    DateTime effectiveAt,
    decimal infrastructure = 500m,
    Guid? id = null,
    DateTime? createdAt = null
  ) =>
    new()
    {
      Id = id ?? Guid.NewGuid(),
      MarketingSharePct = pct,
      Infrastructure = infrastructure,
      RecoveryPerBoost = 10m,
      RecoveryPerTicket = 3m,
      EffectiveAt = effectiveAt,
      CreatedAt = createdAt ?? effectiveAt,
    };

  private static InvoicePartnerChange Partner(
    string suffix,
    DateTime effectiveAt,
    string? name = null,
    InvoiceRoundingPreference rounding = InvoiceRoundingPreference.Down,
    bool active = true,
    int position = 0,
    DateTime? createdAt = null
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      Suffix = suffix,
      Name = name ?? suffix,
      RoundingPreference = rounding,
      Active = active,
      Position = position,
      EffectiveAt = effectiveAt,
      CreatedAt = createdAt ?? effectiveAt,
    };

  private static InvoicePartnerChange[] TwoPartners(DateTime at) =>
    [
      Partner("C", at, "CLEON", InvoiceRoundingPreference.Down, position: 0),
      Partner("Z", at, "ZOEY", InvoiceRoundingPreference.Up, position: 1),
    ];

  // ---- EffectiveSettings ----

  [Fact]
  public void Nothing_effective_yet_is_null()
  {
    InvoiceSettingsSchedule.EffectiveSettings([Settings(50m, Sep)], Jun).Should().BeNull();
  }

  [Fact]
  public void The_newest_effective_row_wins()
  {
    var changes = new[] { Settings(50m, Jan), Settings(40m, Jun), Settings(30m, Sep) };

    InvoiceSettingsSchedule.EffectiveSettings(changes, Jun)!
      .MarketingSharePct.Should()
      .Be(40m);
  }

  [Fact]
  public void A_later_change_never_reaches_back_into_a_settled_month()
  {
    // this is the whole reason the table is insert-only: June was invoiced at
    // 50%, and dropping the share in September must not restate June
    var changes = new[] { Settings(50m, Jan), Settings(30m, Sep) };

    InvoiceSettingsSchedule.EffectiveSettings(changes, Jun)!
      .MarketingSharePct.Should()
      .Be(50m);
  }

  [Fact]
  public void Two_rows_effective_at_the_same_instant_resolve_by_when_they_were_entered()
  {
    var early = Settings(50m, Jun, createdAt: Jan);
    var late = Settings(45m, Jun, createdAt: Sep);

    InvoiceSettingsSchedule.EffectiveSettings([early, late], Sep)!
      .MarketingSharePct.Should()
      .Be(45m);
  }

  [Fact]
  public void A_row_effective_at_exactly_the_asked_instant_counts()
  {
    InvoiceSettingsSchedule.EffectiveSettings([Settings(50m, Jun)], Jun).Should().NotBeNull();
  }

  // ---- EffectivePartners ----

  [Fact]
  public void Partners_come_back_in_position_order()
  {
    var partners = new[]
    {
      Partner("Z", Jan, "ZOEY", position: 1),
      Partner("C", Jan, "CLEON", position: 0),
    };

    InvoiceSettingsSchedule.EffectivePartners(partners, Jun)
      .Select(x => x.Suffix)
      .Should()
      .Equal("C", "Z");
  }

  [Fact]
  public void Each_partner_resolves_independently()
  {
    // Cleon's rounding changes in June; Zoey's January row is untouched and
    // must not need re-entering
    var partners = new[]
    {
      Partner("C", Jan, "CLEON", InvoiceRoundingPreference.Down, position: 0),
      Partner("Z", Jan, "ZOEY", InvoiceRoundingPreference.Up, position: 1),
      Partner("C", Jun, "CLEON", InvoiceRoundingPreference.Up, position: 0),
    };

    var effective = InvoiceSettingsSchedule.EffectivePartners(partners, Sep);

    effective.Should().HaveCount(2);
    effective[0].RoundingPreference.Should().Be(InvoiceRoundingPreference.Up);
    effective[1].RoundingPreference.Should().Be(InvoiceRoundingPreference.Up, "Zoey unchanged");
  }

  [Fact]
  public void A_retired_partner_drops_out_without_losing_history()
  {
    var partners = new[]
    {
      Partner("C", Jan, "CLEON", position: 0),
      Partner("Z", Jan, "ZOEY", position: 1),
      Partner("Z", Jun, "ZOEY", active: false, position: 1),
    };

    InvoiceSettingsSchedule.EffectivePartners(partners, Sep)
      .Select(x => x.Suffix)
      .Should()
      .Equal("C");

    // and the earlier period still has them
    InvoiceSettingsSchedule.EffectivePartners(partners, Jan)
      .Select(x => x.Suffix)
      .Should()
      .Equal("C", "Z");
  }

  [Fact]
  public void A_retired_partner_can_be_brought_back()
  {
    var partners = new[]
    {
      Partner("C", Jan, "CLEON", position: 0),
      Partner("C", Jun, "CLEON", active: false, position: 0),
      Partner("C", Sep, "CLEON", position: 0),
    };

    InvoiceSettingsSchedule.EffectivePartners(partners, Sep).Should().ContainSingle();
  }

  // ---- EffectiveTerms ----

  [Fact]
  public void Terms_need_both_halves()
  {
    // settings with nobody to pay would compute a pool and divide by zero
    InvoiceSettingsSchedule.EffectiveTerms([Settings(50m, Jan)], [], Jun).Should().BeNull();

    // and partners with no agreed share have no pool to divide
    InvoiceSettingsSchedule.EffectiveTerms([], TwoPartners(Jan), Jun).Should().BeNull();
  }

  [Fact]
  public void Terms_carry_the_settings_and_the_partner_set_together()
  {
    var terms = InvoiceSettingsSchedule.EffectiveTerms(
      [Settings(50m, Jan)],
      TwoPartners(Jan),
      Jun
    );

    terms.Should().NotBeNull();
    terms!.MarketingSharePct.Should().Be(50m);
    terms.Infrastructure.Should().Be(500m);
    terms.RecoveryPerBoost.Should().Be(10m);
    terms.RecoveryPerTicket.Should().Be(3m);
    terms.Partners.Select(x => x.Name).Should().Equal("CLEON", "ZOEY");
  }

  // ---- View ----

  [Fact]
  public void The_view_separates_what_is_live_from_what_is_queued()
  {
    var settings = new[] { Settings(50m, Jan), Settings(40m, Sep) };
    var partners = TwoPartners(Jan).Append(Partner("K", Sep, "KIRIN", position: 2)).ToArray();

    var view = InvoiceSettingsSchedule.View(settings, partners, Jun);

    view.Current!.MarketingSharePct.Should().Be(50m);
    view.Current.Partners.Should().HaveCount(2, "the third partner starts in September");
    view.Upcoming.Should().ContainSingle().Which.MarketingSharePct.Should().Be(40m);
    view.UpcomingPartners.Should().ContainSingle().Which.Suffix.Should().Be("K");
  }

  [Fact]
  public void An_unconfigured_system_reports_null_rather_than_zero()
  {
    var view = InvoiceSettingsSchedule.View([], [], Jun);

    view.Current.Should().BeNull();
    view.Upcoming.Should().BeEmpty();
    view.UpcomingPartners.Should().BeEmpty();
  }
}
