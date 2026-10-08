namespace Domain.Exceptions;

// The refundable pool needs the gateway's own refund record for every funding
// payment (refunds an admin issued by hand on the dashboard exist only
// there), and the gateway could not answer. Raised instead of falling back to
// zinc's fragment table alone: that number ignores hand-issued refunds, and a
// card-refund withdrawal planned against it can carve a refund the gateway
// will refuse. Money safety over availability — the caller retries later.
public class RefundGatewayUnavailableException(string? message, Exception? inner)
  : Exception(message, inner);
