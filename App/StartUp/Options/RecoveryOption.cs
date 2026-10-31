using System.ComponentModel.DataAnnotations;
using Domain.Booking;

namespace App.StartUp.Options;

public class RecoveryOption
{
  public const string Key = "Recovery";

  // maximum times a booking may be recycled from Recovering back to Pending
  // (RecoverRevert) before the recycle is refused and the booking ends as
  // Duplicate
  [Required, Range(0, 1000)]
  public int MaxRetries { get; set; } = 10;

  // every retry must happen within this many minutes of the booking's FIRST
  // duplicate-passport rejection (its first Recovering entry); past it the
  // recycle is refused and the booking ends as Duplicate
  [Required, Range(1, 10080)]
  public int MaxWindowMinutes { get; set; } = 120;

  // no recycle when departure is closer than this: tin's reserver never buys
  // inside its closing buffer (tin buffers.closing, 120 min), so a recycled
  // booking would only sit in Pending until the post-departure refund. Keep
  // >= tin's buffers.closing.
  [Required, Range(0, 10080)]
  public int MinDepartureLeadMinutes { get; set; } = 120;

  public RecoveryPolicy ToPolicy() =>
    new()
    {
      MaxRetries = this.MaxRetries,
      MaxWindow = TimeSpan.FromMinutes(this.MaxWindowMinutes),
      MinDepartureLead = TimeSpan.FromMinutes(this.MinDepartureLeadMinutes),
    };
}
