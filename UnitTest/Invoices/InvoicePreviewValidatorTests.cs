using App.Modules.Invoices.API.V1;
using FluentAssertions;

namespace UnitTest.Invoices;

// What POST Invoice/preview refuses.
//
// The line these tests draw: the operator is ALLOWED to preview a bad month —
// a loss, an odd correction, a deliberate what-if. What is refused is a
// payload the engine cannot make sense of. Rejecting a legitimate what-if
// would push the operator back to spreadsheets, which is the thing this whole
// build exists to stop.
public class InvoicePreviewValidatorTests
{
  private static readonly PreviewInvoiceReqValidator Validator = new();

  private static PreviewInvoiceReq Valid() =>
    new(
      new PreviewPeriodReq("1-30 Sep 2026", "September 2026", "0901"),
      "02-10-2026",
      "16-10-2026",
      [new PreviewTopupReq("2026-09-01", 1000m, 320m)],
      null,
      new PreviewFeesReq(100m, 200m),
      10_000m,
      0m,
      [
        new PreviewRouteReq("jbw", "JB -> WOODLANDS", "JB->W", 100, 1000m, 5m,
          new PreviewTerminatedReq(0, 0m, 0m)),
      ],
      new PreviewWithdrawalsReq(0, 0m),
      500m,
      50m,
      [
        new PreviewPartnerReq("C", "CLEON", "down"),
        new PreviewPartnerReq("Z", "ZOEY", "up"),
      ],
      new PreviewPriorityReq(new Dictionary<string, PreviewPriorityRouteReq>(), 0m, 0),
      new PreviewSurchargeReq(
        new PreviewCoverageReq(100, 100),
        new Dictionary<string, PreviewPriceLineReq[]>()
      ),
      new PreviewWithdrawalFeeReq(0m, 0, 0),
      new PreviewPromotionalReq(0, 0m),
      0m,
      new PreviewDuplicatesReq(0, 0m),
      null,
      null,
      null
    );

  [Fact]
  public void A_well_formed_month_is_accepted()
  {
    Validator.Validate(Valid()).IsValid.Should().BeTrue();
  }

  // ---- what is refused ----------------------------------------------------

  [Fact]
  public void A_month_with_no_routes_is_refused()
  {
    Validator.Validate(Valid() with { Routes = [] }).IsValid.Should().BeFalse();
  }

  [Fact]
  public void A_route_with_no_key_is_refused()
  {
    // The key joins routes to their priority and surcharge lines. Without it
    // those silently attach to nothing.
    var req = Valid();
    var broken = req with { Routes = [req.Routes[0] with { Key = "" }] };
    Validator.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void A_month_with_no_partners_is_refused()
  {
    Validator.Validate(Valid() with { Partners = [] }).IsValid.Should().BeFalse();
  }

  [Fact]
  public void Two_partners_sharing_a_suffix_are_refused()
  {
    // The suffix is the allocation key AND the letter on the invoice
    // reference. A collision would pay one of them twice and the other never.
    var broken = Valid() with
    {
      Partners =
      [
        new PreviewPartnerReq("C", "CLEON", "down"),
        new PreviewPartnerReq("c", "CLONE", "up"),
      ],
    };
    Validator.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void An_unknown_rounding_preference_is_refused()
  {
    var broken = Valid() with { Partners = [new PreviewPartnerReq("C", "CLEON", "sideways")] };
    Validator.Validate(broken).IsValid.Should().BeFalse();
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(101)]
  public void A_share_outside_zero_to_a_hundred_is_refused(int pct)
  {
    Validator.Validate(Valid() with { MarketingSharePct = pct }).IsValid.Should().BeFalse();
  }

  [Fact]
  public void Coverage_above_the_total_is_refused()
  {
    // Impossible, and it would print a coverage above 100%.
    var broken = Valid() with
    {
      Surcharge = new PreviewSurchargeReq(
        new PreviewCoverageReq(200, 100),
        new Dictionary<string, PreviewPriceLineReq[]>()
      ),
    };
    Validator.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void An_unknown_price_line_kind_is_refused()
  {
    var broken = Valid() with
    {
      Surcharge = new PreviewSurchargeReq(
        new PreviewCoverageReq(100, 100),
        new Dictionary<string, PreviewPriceLineReq[]>
        {
          ["jbw"] = [new PreviewPriceLineReq("bonus", "Mystery line", 1, 5m)],
        }
      ),
    };
    Validator.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void A_negative_recovery_rate_is_refused()
  {
    // A negative rate would pay the partner MORE for money they already
    // collected outside the system.
    var broken = Valid() with { PartnerRecovery = new PreviewRecoveryReq(0, 10, -3m, 3m) };
    Validator.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void A_negative_ticket_count_is_refused()
  {
    var req = Valid();
    var broken = req with { Routes = [req.Routes[0] with { Tickets = -1 }] };
    Validator.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void A_fee_rate_override_outside_zero_to_a_hundred_is_refused()
  {
    Validator.Validate(Valid() with { FeeRateOverride = 150m }).IsValid.Should().BeFalse();
  }

  // ---- what is deliberately ALLOWED ---------------------------------------

  [Fact]
  public void A_loss_making_month_is_allowed()
  {
    // Infrastructure larger than the whole month's revenue. Real, and the
    // operator must be able to see it.
    Validator.Validate(Valid() with { Infrastructure = 1_000_000m }).IsValid.Should().BeTrue();
  }

  [Fact]
  public void A_negative_net_transfer_is_allowed()
  {
    // Signed on purpose: negative means money came back to BunnyBooker.
    // June's was -55.00.
    Validator.Validate(Valid() with { NetTransfers = -55m }).IsValid.Should().BeTrue();
  }

  [Fact]
  public void Incomplete_surcharge_coverage_is_allowed()
  {
    // Below 100% the figures are understated, and that is REPORTED rather
    // than refused — July and earlier genuinely have partial coverage.
    var partial = Valid() with
    {
      Surcharge = new PreviewSurchargeReq(
        new PreviewCoverageReq(40, 100),
        new Dictionary<string, PreviewPriceLineReq[]>()
      ),
    };
    Validator.Validate(partial).IsValid.Should().BeTrue();
  }

  [Fact]
  public void A_month_with_no_partner_recovery_is_allowed()
  {
    // June had none.
    Validator.Validate(Valid() with { PartnerRecovery = null }).IsValid.Should().BeTrue();
  }

  [Fact]
  public void A_zero_share_is_allowed()
  {
    // 0% pays the partners nothing. Odd, but it is a real term someone could
    // agree, and the settings queue already allows it.
    Validator.Validate(Valid() with { MarketingSharePct = 0m }).IsValid.Should().BeTrue();
  }

  [Fact]
  public void The_real_issued_months_all_validate()
  {
    // The strongest statement available: whatever rules are added here, the
    // three months already invoiced must never stop being valid input.
    foreach (var month in new[] { "2026-06", "2026-07", "2026-08" })
    {
      var req = Valid() with
      {
        Routes = InvoiceFixture.Input(month).Routes
          .Select(rt => new PreviewRouteReq(
            rt.Key, rt.Label, rt.Short, rt.Tickets, rt.Revenue, rt.FareRm,
            new PreviewTerminatedReq(
              rt.Terminated.Count, rt.Terminated.KeptRevenue, rt.Terminated.HalfFareSgd)))
          .ToArray(),
      };
      Validator.Validate(req).IsValid.Should().BeTrue("{0} must stay valid", month);
    }
  }

  // ---- the backfill path --------------------------------------------------
  //
  // POST Invoice/transcribe is the ONE write path that stores figures this
  // engine did not produce, so its validator is worth its own tests. The
  // Amounts rules in particular are written as whole-object Must rather than
  // RuleForEach over the dictionary: FluentValidation cannot infer a property
  // name from a projection and throws InvalidOperationException at runtime
  // instead of returning 400. That exact mistake already shipped once in this
  // module and was caught only by a test like these.

  private static readonly TranscribeInvoiceReqValidator Transcribe = new();

  private static TranscribeInvoiceReq ValidTranscribe() =>
    new(
      "01-08-2026",
      "0801",
      "02-09-2026",
      "16-09-2026",
      new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
      Valid(),
      new TranscribeAttestReq(
        5265,
        53219m,
        37471.51m,
        new Dictionary<string, decimal> { ["C"] = 9159.38m, ["Z"] = 9159.38m }
      )
    );

  [Fact]
  public void A_well_formed_transcription_is_accepted()
  {
    Transcribe.Validate(ValidTranscribe()).IsValid.Should().BeTrue();
  }

  [Fact]
  public void A_transcription_naming_no_partners_is_refused()
  {
    // The check that makes this path safe compares partner amounts. With an
    // empty dictionary it would compare nothing and pass anything.
    var broken = ValidTranscribe() with
    {
      Attest = ValidTranscribe().Attest with { Amounts = new Dictionary<string, decimal>() },
    };
    Transcribe.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void A_transcription_with_a_blank_partner_suffix_is_refused()
  {
    var broken = ValidTranscribe() with
    {
      Attest = ValidTranscribe().Attest with
      {
        Amounts = new Dictionary<string, decimal> { [" "] = 9159.38m },
      },
    };
    Transcribe.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void A_transcription_of_a_month_with_no_tickets_is_refused()
  {
    // Allowed in a preview as a what-if; not allowed as a record of a month
    // somebody was actually invoiced for.
    var broken = ValidTranscribe() with
    {
      Attest = ValidTranscribe().Attest with { Tickets = 0 },
    };
    Transcribe.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void A_transcription_issued_in_the_future_is_refused()
  {
    // The giveaway for a caller that meant to issue a new invoice and reached
    // for the backfill endpoint instead.
    var broken = ValidTranscribe() with { IssuedAt = DateTime.UtcNow.AddDays(30) };
    Transcribe.Validate(broken).IsValid.Should().BeFalse();
  }

  [Fact]
  public void A_transcription_with_an_unparseable_date_is_refused()
  {
    // Every date is ParseExact'd in the mapper, so an unparseable one would
    // throw out of the mapper rather than come back as a 400.
    Transcribe
      .Validate(ValidTranscribe() with { PeriodMonth = "2026-08-01" })
      .IsValid.Should()
      .BeFalse();
  }

  [Fact]
  public void A_transcription_due_before_it_was_issued_is_refused()
  {
    var broken = ValidTranscribe() with { DueDate = "01-09-2026", IssueDate = "02-09-2026" };
    Transcribe.Validate(broken).IsValid.Should().BeFalse();
  }
}
