namespace Domain.Booking;

// Why a booking's recovery retries are over. The duplicate-passport retry
// loop (Recovering -> RecoverRevert -> Pending -> re-buy -> Recovering ...)
// stops on whichever comes first:
//   - Retries:   it was recycled MaxRetries times;
//   - Window:    MaxWindow has elapsed since the FIRST time it was parked in
//                Recovering (its first duplicate-passport rejection);
//   - Departure: departure is closer than MinDepartureLead — tin's reserver
//                never buys inside its closing buffer, so a recycled booking
//                could only sit in Pending until the post-departure refund.
public enum RecoveryExhaustion
{
  Retries,
  Window,
  Departure,
}

// The retry budget for one booking. Pure: the service and the bulk expiry
// sweep evaluate it the same way, and the unit tests pin the edges.
public record RecoveryPolicy
{
  public required int MaxRetries { get; init; }

  public required TimeSpan MaxWindow { get; init; }

  public required TimeSpan MinDepartureLead { get; init; }

  // The reason the booking may not be recycled again at `now`, or null while
  // it still has budget. countRetries = false skips the retry cap — used by
  // the Pending expiry, where the last allowed recycle is still in flight and
  // only the clock (window / departure) may end it.
  public RecoveryExhaustion? Exhausted(BookingPrincipal b, DateTimeOffset now, bool countRetries)
  {
    if (countRetries && b.RecoveryRetries >= this.MaxRetries)
      return RecoveryExhaustion.Retries;

    // rows parked before the stamp existed have no start: never guess a
    // window for them (the next Recovering transition stamps it)
    var first = b.Status.FirstRecoveringAt;
    if (first != null && now.UtcDateTime - DateTime.SpecifyKind(first.Value, DateTimeKind.Utc) >= this.MaxWindow)
      return RecoveryExhaustion.Window;

    if (BookingPurchaseTiming.LeadTime(b.Record.Date, b.Record.Time, now) < this.MinDepartureLead)
      return RecoveryExhaustion.Departure;

    return null;
  }
}
