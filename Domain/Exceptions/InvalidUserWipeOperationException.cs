namespace Domain.Exceptions;

// the PDPA account wipe was refused: the wallet still holds money, a payout
// is still in flight, or the user is already wiped — maps to 409 at the edge
public class InvalidUserWipeOperationException(string? message, string userId, string reason)
  : Exception(message)
{
  public string UserId { get; init; } = userId;

  // machine-readable refusal: "wallet_not_empty", "withdrawal_in_flight" or
  // "already_wiped"
  public string Reason { get; init; } = reason;
}
