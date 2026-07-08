namespace Domain.Exceptions;

// A payout webhook event refers to a superseded attempt or an already-settled
// withdrawal: it must be acknowledged (2xx) so the gateway stops redelivering,
// but it must not mutate any state. The webhook layer converts this into a
// logged no-op.
public class StalePayoutEventException(string? message) : Exception(message);
