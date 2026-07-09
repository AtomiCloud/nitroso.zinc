using Domain.Cost;
using Domain.Discount;
using Domain.Timings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Discounts;

// Discount slot targeting mirrors CostPolicyMatcher.Applies exactly: every
// Match* dimension, their combinations, the lead-time boundary (incl. the
// "departed slot never matches" guard) and the half-open effective window.
// CRITICAL: with no spec (the Cost/self path) a discount with ANY slot
// matcher set must conservatively never match.
public class DiscountSlotMatcherTests
{
  // 2026-07-15 is a Wednesday; departure UTC = 2026-07-15 00:30 (SGT -8h)
  private static readonly BookingCostSpec Spec = new()
  {
    Date = new DateOnly(2026, 7, 15),
    Time = new TimeOnly(8, 30),
    Direction = TrainDirection.JToW,
  };

  private static readonly DateTime NowUtc = new(2026, 7, 8, 0, 30, 0, DateTimeKind.Utc);

  private static DiscountRecord Discount(
    DateOnly? matchDate = null,
    TimeOnly? matchTime = null,
    DayOfWeek? matchDayOfWeek = null,
    TrainDirection? matchDirection = null,
    int? leadTimeUnderHours = null,
    DateTime? effectiveAt = null,
    DateTime? expiresAt = null
  ) =>
    new()
    {
      Name = "discount",
      Description = "a discount",
      Amount = 1m,
      Type = DiscountType.Flat,
      MatchDate = matchDate,
      MatchTime = matchTime,
      MatchDayOfWeek = matchDayOfWeek,
      MatchDirection = matchDirection,
      LeadTimeUnderHours = leadTimeUnderHours,
      EffectiveAt = effectiveAt,
      ExpiresAt = expiresAt,
    };

  [Fact]
  public void All_null_matches_everything()
  {
    DiscountSlotMatcher.Applies(Discount(), Spec, NowUtc).Should().BeTrue();
  }

  [Fact]
  public void All_null_also_matches_the_specless_self_price()
  {
    DiscountSlotMatcher.Applies(Discount(), null, NowUtc).Should().BeTrue();
  }

  [Fact]
  public void Date_dimension_matches_exactly()
  {
    DiscountSlotMatcher.Applies(Discount(matchDate: Spec.Date), Spec, NowUtc).Should().BeTrue();
    DiscountSlotMatcher
      .Applies(Discount(matchDate: Spec.Date.AddDays(1)), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Time_dimension_matches_exactly()
  {
    DiscountSlotMatcher.Applies(Discount(matchTime: Spec.Time), Spec, NowUtc).Should().BeTrue();
    DiscountSlotMatcher
      .Applies(Discount(matchTime: new TimeOnly(9, 45)), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void DayOfWeek_dimension_matches_the_travel_dates_weekday()
  {
    DiscountSlotMatcher
      .Applies(Discount(matchDayOfWeek: DayOfWeek.Wednesday), Spec, NowUtc)
      .Should()
      .BeTrue();
    DiscountSlotMatcher
      .Applies(Discount(matchDayOfWeek: DayOfWeek.Saturday), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Direction_dimension_matches_exactly()
  {
    DiscountSlotMatcher
      .Applies(Discount(matchDirection: TrainDirection.JToW), Spec, NowUtc)
      .Should()
      .BeTrue();
    DiscountSlotMatcher
      .Applies(Discount(matchDirection: TrainDirection.WToJ), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Combined_dimensions_all_must_match()
  {
    DiscountSlotMatcher
      .Applies(
        Discount(matchDayOfWeek: DayOfWeek.Wednesday, matchDirection: TrainDirection.JToW),
        Spec,
        NowUtc
      )
      .Should()
      .BeTrue();
    // one matches, one does not: AND semantics
    DiscountSlotMatcher
      .Applies(
        Discount(matchDayOfWeek: DayOfWeek.Wednesday, matchDirection: TrainDirection.WToJ),
        Spec,
        NowUtc
      )
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Lead_time_boundary_is_inclusive()
  {
    // departure UTC is 2026-07-15 00:30; now exactly 24h before
    var now = new DateTime(2026, 7, 14, 0, 30, 0, DateTimeKind.Utc);
    DiscountSlotMatcher.Applies(Discount(leadTimeUnderHours: 24), Spec, now).Should().BeTrue();

    // one second earlier the lead exceeds 24h — no longer "under"
    DiscountSlotMatcher
      .Applies(Discount(leadTimeUnderHours: 24), Spec, now.AddSeconds(-1))
      .Should()
      .BeFalse();
  }

  [Fact]
  public void A_departed_slot_never_matches_a_lead_time_discount()
  {
    // now is AFTER the departure instant: negative lead must never match
    var afterDeparture = new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc);
    DiscountSlotMatcher
      .Applies(Discount(leadTimeUnderHours: 24), Spec, afterDeparture)
      .Should()
      .BeFalse("a booking for the past has no lead time at all");
  }

  [Fact]
  public void Effective_window_is_half_open()
  {
    var effective = NowUtc.AddHours(-1);
    var expires = NowUtc.AddHours(1);

    DiscountSlotMatcher
      .Applies(Discount(effectiveAt: effective, expiresAt: expires), Spec, NowUtc)
      .Should()
      .BeTrue();

    // exactly at EffectiveAt: inclusive
    DiscountSlotMatcher
      .Applies(Discount(effectiveAt: NowUtc, expiresAt: expires), Spec, NowUtc)
      .Should()
      .BeTrue();

    // exactly at ExpiresAt: exclusive
    DiscountSlotMatcher
      .Applies(Discount(effectiveAt: effective, expiresAt: NowUtc), Spec, NowUtc)
      .Should()
      .BeFalse();
  }

  [Fact]
  public void Not_yet_effective_or_expired_discounts_never_apply()
  {
    DiscountSlotMatcher
      .Applies(Discount(effectiveAt: NowUtc.AddDays(1)), Spec, NowUtc)
      .Should()
      .BeFalse("the discount only starts tomorrow");
    DiscountSlotMatcher
      .Applies(Discount(expiresAt: NowUtc.AddDays(-1)), Spec, NowUtc)
      .Should()
      .BeFalse("the discount expired yesterday");
  }

  [Fact]
  public void The_window_applies_even_without_a_spec()
  {
    DiscountSlotMatcher
      .Applies(Discount(effectiveAt: NowUtc.AddDays(1)), null, NowUtc)
      .Should()
      .BeFalse("a not-yet-effective discount never applies, spec or not");
    DiscountSlotMatcher
      .Applies(Discount(effectiveAt: NowUtc.AddDays(-1), expiresAt: NowUtc.AddDays(1)), null, NowUtc)
      .Should()
      .BeTrue("the window is not a slot dimension — it works on the self price");
  }

  // ---- spec == null conservatism: EVERY slot dimension blocks alone ----

  public static TheoryData<DiscountRecord> SlotTargetedDiscounts() =>
    new()
    {
      Discount(matchDate: new DateOnly(2026, 7, 15)),
      Discount(matchTime: new TimeOnly(8, 30)),
      Discount(matchDayOfWeek: DayOfWeek.Wednesday),
      Discount(matchDirection: TrainDirection.JToW),
      Discount(leadTimeUnderHours: 24),
      Discount(matchDate: new DateOnly(2026, 7, 15), matchDirection: TrainDirection.JToW),
    };

  [Theory]
  [MemberData(nameof(SlotTargetedDiscounts))]
  public void Any_slot_matcher_blocks_the_specless_self_price(DiscountRecord record)
  {
    DiscountSlotMatcher
      .Applies(record, null, NowUtc)
      .Should()
      .BeFalse("Cost/self is not slot-specific — slot-targeted discounts must never leak into it");

    // sanity: the same discount DOES match its own slot (30 min before
    // departure so the lead-time entry is inside its cap too)
    var justBeforeDeparture = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
    DiscountSlotMatcher.Applies(record, Spec, justBeforeDeparture).Should().BeTrue();
  }

  // ---- the full matcher: user/role targeting AND slot targeting ----

  [Fact]
  public void Match_requires_both_the_target_and_the_slot_to_match()
  {
    var matcher = new DiscountMatcher(NullLogger<DiscountMatcher>.Instance);
    var roleTarget = new DiscountTarget
    {
      MatchMode = DiscountMatchMode.Any,
      Matches = [new DiscountMatch { Type = DiscountMatchType.Role, Value = "vip" }],
    };
    var slotDiscount = Discount(matchDirection: TrainDirection.JToW);

    matcher
      .Match(roleTarget, slotDiscount, "user-1", ["vip"], Spec, NowUtc)
      .Should()
      .BeTrue("role matches and the slot matches");
    matcher
      .Match(roleTarget, slotDiscount, "user-1", [], Spec, NowUtc)
      .Should()
      .BeFalse("the slot matches but the role does not");
    matcher
      .Match(
        roleTarget,
        slotDiscount,
        "user-1",
        ["vip"],
        Spec with
        {
          Direction = TrainDirection.WToJ,
        },
        NowUtc
      )
      .Should()
      .BeFalse("the role matches but the slot does not");
    matcher
      .Match(roleTarget, slotDiscount, "user-1", ["vip"], null, NowUtc)
      .Should()
      .BeFalse("slot-targeted discounts never match the spec-less self price");
  }

  [Fact]
  public void User_and_role_targeting_semantics_are_unchanged()
  {
    var matcher = new DiscountMatcher(NullLogger<DiscountMatcher>.Instance);
    var noSlots = Discount();

    var all = new DiscountTarget
    {
      MatchMode = DiscountMatchMode.All,
      Matches =
      [
        new DiscountMatch { Type = DiscountMatchType.UserId, Value = "user-1" },
        new DiscountMatch { Type = DiscountMatchType.Role, Value = "vip" },
      ],
    };
    matcher.Match(all, noSlots, "user-1", ["vip"], null, NowUtc).Should().BeTrue();
    matcher.Match(all, noSlots, "user-1", [], null, NowUtc).Should().BeFalse();

    var none = new DiscountTarget { MatchMode = DiscountMatchMode.None, Matches = [] };
    matcher.Match(none, noSlots, "anyone", [], null, NowUtc).Should().BeTrue();
  }
}
