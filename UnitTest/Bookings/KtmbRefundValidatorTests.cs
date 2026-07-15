using App.Modules.Bookings.API.V1;
using FluentAssertions;

namespace UnitTest.Bookings;

// The request validation around the KTMB termination-refund capture: the
// PINNED refund body (refundAmount >= 0, refundCurrency 3-8 chars), the
// Status selector the cost worklist gained (2 = Completed, 5 = Terminated),
// and the refund worklist paging.
public class KtmbRefundValidatorTests
{
  // ---- the refund capture body ----

  private readonly SetBookingKtmbRefundReqValidator refundValidator = new();

  [Theory]
  [InlineData(0, "MYR")]
  [InlineData(21.3, "MYR")]
  [InlineData(21.3, "SGD")]
  [InlineData(10_000_000, "POINTS")]
  public void Refund_with_non_negative_amount_and_sane_currency_is_valid(
    decimal amount,
    string currency
  )
  {
    // zero included — KTMB can refund nothing, and recording that fact takes
    // the booking off the refund worklist
    refundValidator
      .Validate(new SetBookingKtmbRefundReq(amount, currency))
      .IsValid.Should()
      .BeTrue();
  }

  [Fact]
  public void Refund_with_negative_amount_is_rejected()
  {
    refundValidator
      .Validate(new SetBookingKtmbRefundReq(-0.01m, "MYR"))
      .IsValid.Should()
      .BeFalse();
  }

  [Fact]
  public void Refund_beyond_the_stored_precision_is_rejected()
  {
    refundValidator
      .Validate(new SetBookingKtmbRefundReq(10_000_001m, "MYR"))
      .IsValid.Should()
      .BeFalse();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("MY")]
  [InlineData("TOOLONGCUR")]
  public void Refund_with_a_currency_outside_3_to_8_chars_is_rejected(string? currency)
  {
    refundValidator
      .Validate(new SetBookingKtmbRefundReq(21.3m, currency!))
      .IsValid.Should()
      .BeFalse();
  }

  // ---- the cost worklist Status selector ----

  private readonly KtmbCostMissingQueryValidator costMissingValidator = new();

  [Theory]
  [InlineData(null)]
  [InlineData(2)]
  [InlineData(5)]
  public void Cost_worklist_status_omitted_completed_or_terminated_is_valid(int? status)
  {
    costMissingValidator
      .Validate(new KtmbCostMissingQuery(100, 0, status))
      .IsValid.Should()
      .BeTrue();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(3)]
  [InlineData(4)]
  [InlineData(6)]
  [InlineData(7)]
  public void Cost_worklist_status_of_any_other_book_status_is_rejected(int status)
  {
    // only the two worklists that exist: the completion backfill (2) and the
    // terminated-then-refunded history (5)
    costMissingValidator
      .Validate(new KtmbCostMissingQuery(100, 0, status))
      .IsValid.Should()
      .BeFalse();
  }

  // ---- the refund worklist paging ----

  private readonly KtmbRefundMissingQueryValidator refundMissingValidator = new();

  [Fact]
  public void Refund_worklist_with_omitted_paging_is_valid()
  {
    refundMissingValidator
      .Validate(new KtmbRefundMissingQuery(null, null))
      .IsValid.Should()
      .BeTrue();
  }

  [Theory]
  [InlineData(-1, 0)]
  [InlineData(101, 0)]
  [InlineData(100, -1)]
  public void Refund_worklist_with_out_of_bounds_paging_is_rejected(int limit, int skip)
  {
    refundMissingValidator
      .Validate(new KtmbRefundMissingQuery(limit, skip))
      .IsValid.Should()
      .BeFalse();
  }
}
