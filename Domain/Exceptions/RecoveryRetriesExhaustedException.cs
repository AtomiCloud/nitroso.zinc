using Domain.Booking;

namespace Domain.Exceptions;

// A 'Recovering' booking has used up its recovery budget: recycled the
// configured maximum number of times, past its retry window, or too close to
// departure to be bought again (Reason says which). Another automated retry is
// refused so a permanently-conflicted booking cannot loop forever. The booking
// stays in 'Recovering' — the recoverer (or a human) decides the terminal
// outcome (Duplicate or manual intervention).
public class RecoveryRetriesExhaustedException(
  string bookingId,
  int retries,
  int maxRetries,
  RecoveryExhaustion reason = RecoveryExhaustion.Retries
)
  : Exception(
    $"Booking '{bookingId}' exhausted its recovery retries ({retries}/{maxRetries}, reason: {reason}); resolve it manually"
  )
{
  public string BookingId { get; } = bookingId;

  public int Retries { get; } = retries;

  public int MaxRetries { get; } = maxRetries;

  public RecoveryExhaustion Reason { get; } = reason;
}
