namespace Domain.Withdrawal;

// Pure refundable-pool arithmetic. Per funding payment:
//
//   refundable = captured − (gateway non-failed refunds
//                            + zinc active fragments the gateway does not show yet)
//
// The gateway's own record is the authority on what already went back to the
// card: refunds an admin issued by hand on the dashboard (the PayNow-era
// practice) exist ONLY there, and planning a fragment against such an intent
// produces a refund the gateway can never create. zinc's fragments are added
// on top only while the gateway does not list them yet (planned but not sent,
// or sent with the response lost), so a fragment that is already at the
// gateway is never counted twice. A fragment is "at the gateway" when one of
// the gateway's non-failed refunds carries its refund id or its request id;
// matching against a FAILED gateway refund deliberately does not count — the
// fragment then still holds its claim until zinc records the failure, which
// errs towards a smaller pool, never a larger one.
public static class RefundPool
{
  public static List<RefundablePayment> Compute(
    IEnumerable<FundingPayment> payments,
    IEnumerable<WithdrawalRefundFragment> activeFragments,
    IReadOnlyDictionary<string, List<GatewayRefund>> gatewayRefundsByIntent
  )
  {
    var fragmentsByPayment = activeFragments
      .Where(f => f.Status != RefundFragmentStatus.Failed)
      .ToLookup(f => f.PaymentId);
    return payments
      .Select(p =>
      {
        var live = gatewayRefundsByIntent
          .GetValueOrDefault(p.PaymentIntentId, [])
          .Where(r => r.Outcome != PayoutOutcome.Failed)
          .ToList();
        var liveIds = live.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var liveRequestIds = live.Where(r => r.RequestId != null)
          .Select(r => r.RequestId!)
          .ToHashSet(StringComparer.Ordinal);
        var notYetAtGateway = fragmentsByPayment[p.PaymentId]
          .Where(f =>
            !(f.AirwallexRefundId != null && liveIds.Contains(f.AirwallexRefundId))
            && !liveRequestIds.Contains(f.RequestId)
          )
          .Sum(f => f.Amount);
        return new RefundablePayment
        {
          PaymentId = p.PaymentId,
          PaymentIntentId = p.PaymentIntentId,
          CreatedAt = p.CreatedAt,
          Refundable = p.CapturedAmount - live.Sum(r => r.Amount) - notYetAtGateway,
        };
      })
      .Where(p => p.Refundable > 0)
      .OrderBy(p => p.CreatedAt)
      .ToList();
  }
}
