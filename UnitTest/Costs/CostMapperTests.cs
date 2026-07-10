using App.Modules.Costs.API.V1;
using Domain.Booking;
using Domain.Cost;
using FluentAssertions;

namespace UnitTest.Costs;

public class CostMapperTests
{
  [Fact]
  public void Batch_slot_exposes_the_same_lossless_quote_as_its_final()
  {
    var slot = new MaterializedCostSlot
    {
      Time = new TimeOnly(8, 30),
      Cost = new MaterializedCost
      {
        Cost = 15.12345678m,
        PolicyLines = [],
        Subtotal = 15.12345678m,
        Discounts = [],
        Final = 13.25636350m,
      },
    };

    var response = slot.ToRes();

    response.Final.Should().Be(13.25636350m);
    response.Quote.Should().Be(BookingPriceQuote.Create(response.Final));
    response.Quote.Should().Be("13.2563635");
  }
}
