namespace Domain.Exceptions;

// A 'Recovering' booking has already been recycled back to 'Pending' the
// configured maximum number of times; another automated retry is refused so a
// permanently-conflicted booking cannot loop forever. The booking stays in
// 'Recovering' — a human decides the terminal outcome (Duplicate or manual
// intervention).
public class RecoveryRetriesExhaustedException(string bookingId, int retries, int maxRetries)
  : Exception(
    $"Booking '{bookingId}' exhausted its recovery retries ({retries}/{maxRetries}); resolve it manually"
  )
{
  public string BookingId { get; } = bookingId;

  public int Retries { get; } = retries;

  public int MaxRetries { get; } = maxRetries;
}
