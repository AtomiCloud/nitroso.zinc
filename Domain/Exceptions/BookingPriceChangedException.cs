namespace Domain.Exceptions;

public class BookingPriceChangedException(string expected, string actual)
  : Exception("The booking price changed. Refresh the quote and confirm again.")
{
  public string Expected { get; } = expected;

  public string Actual { get; } = actual;
}
