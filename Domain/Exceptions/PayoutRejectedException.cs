namespace Domain.Exceptions;

// The payout gateway DEFINITIVELY refused to create the payout (a validation
// rejection): no transfer exists for the request id, so the withdrawal may
// safely return to Pending and a later attempt may use a fresh request id.
// Any failure that does NOT prove non-creation (timeout, 5xx, connection
// reset) must NOT use this type — those are ambiguous and the withdrawal has
// to stay claimed so the retry reuses the same request id.
public class PayoutRejectedException(string? message) : Exception(message);
