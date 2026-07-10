using System.ComponentModel;
using System.Text.Json.Serialization;

namespace App.Error.V1;

[Description(
  "The booking price changed between the quote the customer confirmed and the purchase; the purchase was rejected without charging"
)]
public class BookingPriceChanged : IDomainProblem
{
  public BookingPriceChanged() { }

  public BookingPriceChanged(string detail, string expected, string actual)
  {
    this.Detail = detail;
    this.Expected = expected;
    this.Actual = actual;
  }

  [JsonIgnore]
  public string Id { get; } = "booking_price_changed";

  [JsonIgnore]
  public string Title { get; } = "Booking Price Changed";

  [JsonIgnore]
  public string Version { get; } = "v1";

  public string Detail { get; } = string.Empty;

  [Description("The canonical quote the customer confirmed")]
  public string Expected { get; } = string.Empty;

  [Description("The canonical price computed at purchase time")]
  public string Actual { get; } = string.Empty;
}
