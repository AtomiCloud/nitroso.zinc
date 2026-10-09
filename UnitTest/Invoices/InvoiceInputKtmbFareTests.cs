using Domain.Booking;
using Domain.Invoice;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Invoices;

// GET Invoice/inputs carries each route's KTMB fare for the INVOICED month:
// the KtmbCosts row in force at the month's last SGT instant. Drafts for a
// past month must price at that month's fare, not today's — the issued
// Jun/Jul/Aug invoices used RM 17.50 / 16.05 / 16.15, which no single
// "current" fare can reproduce.
public class InvoiceInputKtmbFareTests
{
  private static readonly DateOnly June = new(2026, 6, 1);
  private static readonly DateOnly July = new(2026, 7, 1);
  private static readonly DateOnly August = new(2026, 8, 1);

  // an SGT wall-clock instant, expressed in UTC (SGT = UTC+8, no DST)
  private static DateTime Sgt(int y, int m, int d, int h = 0, int min = 0) =>
    DateTime.SpecifyKind(new DateTime(y, m, d, h, min, 0).AddHours(-8), DateTimeKind.Utc);

  private static KtmbCostChange Change(
    TrainDirection direction,
    decimal cost,
    DateTime effectiveAt,
    DateTime? createdAt = null
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      Direction = direction,
      Cost = cost,
      EffectiveAt = effectiveAt,
      CreatedAt = createdAt ?? effectiveAt,
    };

  private static decimal? Fare(InvoiceInputRow row, InvoiceDirection dir) =>
    row.Routes.Single(r => r.Direction == dir).KtmbFare;

  [Fact]
  public void MonthEndUtc_IsTheLastInstantOfTheSgtMonth()
  {
    // 30 Jun 23:59:59.9999999 SGT = 30 Jun 15:59:59.9999999 UTC
    var end = InvoiceInputCalculator.MonthEndUtc(June);

    end.Kind.Should().Be(DateTimeKind.Utc);
    end.Should().Be(Sgt(2026, 7, 1).AddTicks(-1));
  }

  [Fact]
  public void NoRows_FareIsNull_NotZero()
  {
    // "never configured" must surface as missing, not as a free ticket
    var row = InvoiceInputCalculator.Gather([], June, []);

    Fare(row, InvoiceDirection.JbToWoodlands).Should().BeNull();
    Fare(row, InvoiceDirection.WoodlandsToJb).Should().BeNull();
  }

  [Fact]
  public void NoScheduleArgument_FareIsNull()
  {
    var row = InvoiceInputCalculator.Gather([], June);

    Fare(row, InvoiceDirection.JbToWoodlands).Should().BeNull();
  }

  [Fact]
  public void EachMonth_GetsItsOwnFare()
  {
    var changes = new[]
    {
      Change(TrainDirection.JToW, 17.50m, Sgt(2026, 6, 1)),
      Change(TrainDirection.JToW, 16.05m, Sgt(2026, 7, 1)),
      Change(TrainDirection.JToW, 16.15m, Sgt(2026, 8, 1)),
    };

    Fare(InvoiceInputCalculator.Gather([], June, changes), InvoiceDirection.JbToWoodlands)
      .Should()
      .Be(17.50m);
    Fare(InvoiceInputCalculator.Gather([], July, changes), InvoiceDirection.JbToWoodlands)
      .Should()
      .Be(16.05m);
    Fare(InvoiceInputCalculator.Gather([], August, changes), InvoiceDirection.JbToWoodlands)
      .Should()
      .Be(16.15m);
  }

  [Fact]
  public void ChangeAtNextMonthsSgtMidnight_DoesNotLeakBackwards()
  {
    // effective 1 Jul 00:00 SGT (= 30 Jun 16:00 UTC): a UTC month boundary
    // would wrongly apply it to June
    var changes = new[]
    {
      Change(TrainDirection.JToW, 10m, Sgt(2026, 5, 1)),
      Change(TrainDirection.JToW, 12m, Sgt(2026, 7, 1)),
    };

    Fare(InvoiceInputCalculator.Gather([], June, changes), InvoiceDirection.JbToWoodlands)
      .Should()
      .Be(10m);
    Fare(InvoiceInputCalculator.Gather([], July, changes), InvoiceDirection.JbToWoodlands)
      .Should()
      .Be(12m);
  }

  [Fact]
  public void ChangeOnTheMonthsLastSgtMinute_AppliesToThatMonth()
  {
    var changes = new[] { Change(TrainDirection.JToW, 11m, Sgt(2026, 6, 30, 23, 59)) };

    Fare(InvoiceInputCalculator.Gather([], June, changes), InvoiceDirection.JbToWoodlands)
      .Should()
      .Be(11m);
  }

  [Fact]
  public void LaterRow_DoesNotApplyToAnEarlierMonth()
  {
    var changes = new[] { Change(TrainDirection.JToW, 16.15m, Sgt(2026, 8, 1)) };

    Fare(InvoiceInputCalculator.Gather([], June, changes), InvoiceDirection.JbToWoodlands)
      .Should()
      .BeNull();
  }

  [Fact]
  public void BackdatedRow_AppliesToThePastMonth()
  {
    // entered in October, effective from 1 Sep SGT: September re-prices
    var october = Sgt(2026, 10, 9, 12);
    var changes = new[]
    {
      Change(TrainDirection.WToJ, 16.15m, Sgt(2026, 8, 1)),
      Change(TrainDirection.WToJ, 16.40m, Sgt(2026, 9, 1), createdAt: october),
    };

    var sep = InvoiceInputCalculator.Gather([], new DateOnly(2026, 9, 1), changes);

    Fare(sep, InvoiceDirection.WoodlandsToJb).Should().Be(16.40m);
    // the month before the backdated row is untouched
    Fare(InvoiceInputCalculator.Gather([], August, changes), InvoiceDirection.WoodlandsToJb)
      .Should()
      .Be(16.15m);
  }

  [Fact]
  public void Directions_AreIndependent()
  {
    var changes = new[]
    {
      Change(TrainDirection.JToW, 17m, Sgt(2026, 6, 1)),
      Change(TrainDirection.WToJ, 15m, Sgt(2026, 6, 1)),
    };

    var row = InvoiceInputCalculator.Gather([], June, changes);

    Fare(row, InvoiceDirection.JbToWoodlands).Should().Be(17m);
    Fare(row, InvoiceDirection.WoodlandsToJb).Should().Be(15m);
  }

  [Fact]
  public void ConfiguredZero_IsZero_NotNull()
  {
    var changes = new[] { Change(TrainDirection.JToW, 0m, Sgt(2026, 6, 1)) };

    Fare(InvoiceInputCalculator.Gather([], June, changes), InvoiceDirection.JbToWoodlands)
      .Should()
      .Be(0m);
  }

  [Fact]
  public void SameEffectiveInstant_NewestEntryWins()
  {
    // a correction re-entered at the same effective date replaces the typo
    var changes = new[]
    {
      Change(TrainDirection.JToW, 1.75m, Sgt(2026, 6, 1), createdAt: Sgt(2026, 7, 2)),
      Change(TrainDirection.JToW, 17.50m, Sgt(2026, 6, 1), createdAt: Sgt(2026, 7, 3)),
    };

    Fare(InvoiceInputCalculator.Gather([], June, changes), InvoiceDirection.JbToWoodlands)
      .Should()
      .Be(17.50m);
  }

  [Fact]
  public void History_ListsEveryChange_NewestEffectiveFirst()
  {
    var a = Change(TrainDirection.JToW, 17.50m, Sgt(2026, 6, 1));
    var b = Change(TrainDirection.WToJ, 16.05m, Sgt(2026, 7, 1));
    var c = Change(TrainDirection.JToW, 99m, Sgt(2026, 12, 1)); // queued future row

    KtmbCostSchedule
      .History([a, c, b])
      .Select(x => x.Id)
      .Should()
      .Equal(c.Id, b.Id, a.Id);
  }
}
