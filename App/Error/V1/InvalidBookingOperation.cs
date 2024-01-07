using System.ComponentModel;
using System.Text.Json.Serialization;
using App.Modules.Bookings.API.V1;
using Azure;
using Domain.Booking;

namespace App.Error.V1;

[Description(
  "The booking operation attempted (Complete, Refund, Cancel, Terminate etc) is not valid for the current booking state")]
public class InvalidBookingOperation : IDomainProblem
{
  public InvalidBookingOperation() { }

  public InvalidBookingOperation(string detail, BookStatus bookStatus, string operation)
  {
    this.Detail = detail;
    this.BookStatus = bookStatus.ToRes();
    this.Operation = operation;
  }

  [JsonIgnore] public string Id { get; } = "invalid_booking_operation";

  [JsonIgnore] public string Title { get; } = "Invalid Booking Operation";

  [JsonIgnore] public string Version { get; } = "v1";

  public string Detail { get; } = string.Empty;

  [Description("The current status of the booking that was invalid for the operation attempted")]
  public string BookStatus { get; } = string.Empty;

  [Description("The operaiton that was invalid")]
  public string Operation { get; } = string.Empty;
}
