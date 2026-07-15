using Domain.Payment;
using FluentAssertions;

namespace UnitTest.Payments;

// Pins the payments-vs-payouts fee attribution the analysis views bucket by:
// paymentFees = Payment + AccountFee (account-level billings are dominated by
// per-attempt gateway/3DS fees — costs of ACCEPTING deposits, absorbed by the
// blended gwRate), payoutFees = Transfer + Refund ONLY. Every source type
// must land in exactly one bucket.
public class GatewayFeeBucketsTests
{
  [Theory]
  [InlineData(GatewayFeeSourceType.Payment, true)]
  [InlineData(GatewayFeeSourceType.AccountFee, true)]
  [InlineData(GatewayFeeSourceType.Transfer, false)]
  [InlineData(GatewayFeeSourceType.Refund, false)]
  public void Payment_side_is_payment_plus_account_fee(GatewayFeeSourceType type, bool expected)
  {
    GatewayFeeBuckets.IsPaymentSide(type).Should().Be(expected);
  }

  [Theory]
  [InlineData(GatewayFeeSourceType.Payment, false)]
  [InlineData(GatewayFeeSourceType.AccountFee, false)]
  [InlineData(GatewayFeeSourceType.Transfer, true)]
  [InlineData(GatewayFeeSourceType.Refund, true)]
  public void Payout_side_is_transfer_plus_refund_only(GatewayFeeSourceType type, bool expected)
  {
    GatewayFeeBuckets.IsPayoutSide(type).Should().Be(expected);
  }

  [Fact]
  public void Every_source_type_lands_in_exactly_one_bucket()
  {
    foreach (var type in Enum.GetValues<GatewayFeeSourceType>())
      (GatewayFeeBuckets.IsPaymentSide(type) ^ GatewayFeeBuckets.IsPayoutSide(type))
        .Should()
        .BeTrue($"{type} must be exactly one of payment-side / payout-side");
  }
}
