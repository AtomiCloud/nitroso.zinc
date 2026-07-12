using Domain.Cost;
using Domain.Discount;
using FluentAssertions;

namespace UnitTest.Costs;

// The persisted purchase price breakdown derived from a materialized price:
// policy lines pass through signed, discount lines mirror
// DiscountCalculator's math (resolved against the SUBTOTAL) and are stored
// negative — so component analytics can rank what makes or loses money.
public class PriceBreakdownFactoryTests
{
  private static DiscountRecord Discount(string name, DiscountType type, decimal amount) =>
    new()
    {
      Name = name,
      Description = string.Empty,
      Amount = amount,
      Type = type,
    };

  [Fact]
  public void Policy_lines_pass_through_signed()
  {
    var cost = new MaterializedCost
    {
      Cost = 14m,
      PolicyLines =
      [
        new CostPolicyLine { Name = "Peak surcharge", Delta = 2m },
        new CostPolicyLine { Name = "Promo", Delta = -1.5m },
      ],
      Subtotal = 14.5m,
      Final = 14.5m,
      Discounts = [],
    };

    var b = cost.ToBreakdown();

    b.BaseCost.Should().Be(14m);
    b.Final.Should().Be(14.5m);
    b.Lines.Should().HaveCount(2);
    b.Lines[0].Kind.Should().Be("policy");
    b.Lines[0].Delta.Should().Be(2m);
    b.Lines[1].Delta.Should().Be(-1.5m);
    // stored invariant: base + policy lines = subtotal
    (b.BaseCost + b.Lines.Sum(l => l.Delta)).Should().Be(14.5m);
  }

  [Fact]
  public void Flat_discounts_store_their_negative_amount()
  {
    var cost = new MaterializedCost
    {
      Cost = 14m,
      PolicyLines = [],
      Subtotal = 14m,
      Final = 11m,
      Discounts = [Discount("Loyalty", DiscountType.Flat, 3m)],
    };

    var b = cost.ToBreakdown();

    var line = b.Lines.Single();
    line.Kind.Should().Be("discount");
    line.Name.Should().Be("Loyalty");
    line.Delta.Should().Be(-3m);
  }

  [Fact]
  public void Percentage_discounts_resolve_against_the_subtotal()
  {
    // mirrors DiscountCalculator: percentage discounts multiply the
    // SUBTOTAL, not the running total
    var cost = new MaterializedCost
    {
      Cost = 14m,
      PolicyLines = [new CostPolicyLine { Name = "Peak", Delta = 6m }],
      Subtotal = 20m,
      Final = 18m,
      Discounts = [Discount("Members", DiscountType.Percentage, 0.1m)],
    };

    var b = cost.ToBreakdown();

    var discount = b.Lines.Single(l => l.Kind == "discount");
    discount.Delta.Should().Be(-2m); // 0.1 × 20
    // subtotal + discount deltas = final (before the >= 0 floor)
    (cost.Subtotal + b.Lines.Where(l => l.Kind == "discount").Sum(l => l.Delta))
      .Should()
      .Be(cost.Final);
  }

  [Fact]
  public void Multiple_discounts_each_resolve_against_the_same_subtotal()
  {
    var cost = new MaterializedCost
    {
      Cost = 10m,
      PolicyLines = [],
      Subtotal = 10m,
      Final = 6m,
      Discounts =
      [
        Discount("A", DiscountType.Percentage, 0.2m),
        Discount("B", DiscountType.Flat, 2m),
      ],
    };

    var b = cost.ToBreakdown();

    b.Lines.Select(l => l.Delta).Should().Equal(-2m, -2m);
  }

  [Fact]
  public void No_components_yield_an_empty_line_list()
  {
    var cost = new MaterializedCost
    {
      Cost = 14m,
      PolicyLines = [],
      Subtotal = 14m,
      Final = 14m,
      Discounts = [],
    };

    var b = cost.ToBreakdown();

    b.Lines.Should().BeEmpty();
    b.BaseCost.Should().Be(14m);
    b.Final.Should().Be(14m);
  }
}
