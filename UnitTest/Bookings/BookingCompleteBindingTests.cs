using System.Reflection;
using App.Modules.Bookings.API.V1;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace UnitTest.Bookings;

// tin posts the actual KTMB cost as multipart form fields next to the ticket
// PDF (lib/buyer/client.go completeOnce). Under [ApiController] an
// unattributed simple-type parameter is inferred [FromQuery], so the fields
// were silently bound to null and every live completion lost its amount —
// pin the binding source so that cannot regress.
public class BookingCompleteBindingTests
{
  private static ParameterInfo Param(string name) =>
    typeof(BookingController)
      .GetMethod(nameof(BookingController.Complete))!
      .GetParameters()
      .Single(p => p.Name == name);

  [Theory]
  [InlineData("ktmbAmount")]
  [InlineData("ktmbCurrency")]
  public void Complete_binds_ktmb_cost_from_the_multipart_form(string name)
  {
    var source = Param(name).GetCustomAttributes().OfType<IBindingSourceMetadata>().SingleOrDefault();

    source.Should().NotBeNull($"{name} must not fall back to the inferred [FromQuery]");
    source!.BindingSource.Should().Be(BindingSource.Form);
  }

  [Fact]
  public void Complete_keeps_booking_and_ticket_numbers_on_the_query()
  {
    // tin sends these as query params (zinc.PostApiVVersionBookingCompleteIdParams)
    foreach (var name in new[] { "bookingNo", "ticketNo" })
      Param(name).GetCustomAttributes().OfType<IBindingSourceMetadata>().Should().BeEmpty();
  }
}
