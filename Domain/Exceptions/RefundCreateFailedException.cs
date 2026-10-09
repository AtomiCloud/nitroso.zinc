namespace Domain.Exceptions;

// The refund gateway failed to create a card refund. Like every refund-create
// failure it is AMBIGUOUS for the withdrawal (sibling fragments may exist), so
// it changes nothing about the retry semantics. What it adds is Diagnostic: a
// short, controlled summary (HTTP status plus the gateway's own error code and
// message, or a fixed text for transport faults) that is safe to persist on
// the fragment and show on the withdrawal. The raw gateway body stays in the
// logs and in Message only.
public class RefundCreateFailedException(string? message, string diagnostic, Exception? inner = null)
  : Exception(message, inner)
{
  public string Diagnostic { get; } = diagnostic;
}
