namespace Domain.Exceptions;

// A card-refund withdrawal needs the user's refundable pool (captured card
// payments inside the refund window, minus refunds already issued against
// them) to cover the net payout. Raised at creation (pool checked before the
// reserve moves) and at approval (the pool may have shrunk in between —
// the claim is reverted to Pending and no refund is created).
public class InsufficientRefundablePoolException(
  string? message,
  decimal required,
  decimal available
) : Exception(message)
{
  // net amount (gross minus fee) the refunds must cover
  public decimal Required { get; } = required;

  // what the refundable pool can still cover
  public decimal Available { get; } = available;
}
