namespace Domain.Exceptions;

public class BookingPriceChangedException(decimal expected, decimal actual)
  : Exception("The booking price changed. Refresh the quote and confirm again.")
{
  public decimal Expected { get; } = expected;

  public decimal Actual { get; } = actual;
}
