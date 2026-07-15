namespace Domain.Booking;

// the Redis 'BookingTermination' queue payload tin's terminator consumes:
// the KTMB identifiers to terminate, plus the zinc booking Id so the
// terminator can capture the KTMB refund back through
// POST Booking/{id}/ktmb-refund (additive — old tin builds ignore it)
public record BookingTermination(string BookingNo, string TicketNo, Guid Id);
