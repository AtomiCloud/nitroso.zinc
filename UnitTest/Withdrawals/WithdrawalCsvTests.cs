using System.Globalization;
using App.Modules.Withdrawals.API.V1;
using Domain.User;
using Domain.Wallet;
using Domain.Withdrawal;
using FluentAssertions;

namespace UnitTest.Withdrawals;

public class WithdrawalCsvTests
{
  private const string ExpectedHeader =
    "withdrawal_id,created_at,completed_at,status,method,user_id,user_email,gross_amount,fee,net_amount,paynow_number,airwallex_confirmation,payout_attempt,reconcile_attempts,completer_id,completion_note,receipt_url,refund_payment_intent_ids,refund_airwallex_ids,refund_amounts,refund_statuses,refund_settled_ats,receipt_key,refund_arns";

  [Fact]
  public void To_export_row_uses_the_stored_payout_fee_for_the_fixed_two_decimal_net_amount()
  {
    var row = MakeWithdrawal(amount: 100m, payout: Payout(1.3332m)).ToExportRow(receiptUrl: null);

    row.GrossAmount.Should().Be("100.00");
    row.Fee.Should().Be("1.33");
    row.NetAmount.Should().Be("98.67", "net is gross minus the fee stored with this payout");
    row.PayoutAttempt.Should().Be("2");
    row.ReconcileAttempts.Should().Be("1");
  }

  [Fact]
  public void To_export_row_without_a_payout_leaves_all_payout_derived_cells_blank()
  {
    // no money moved on a pending withdrawal, so every payout-derived cell —
    // money columns included — is blank
    var row = MakeWithdrawal(payout: null, status: WithdrawStatus.Pending)
      .ToExportRow(receiptUrl: null);

    row.Fee.Should().BeEmpty();
    row.NetAmount.Should().BeEmpty();
    row.AirwallexConfirmation.Should().BeEmpty();
    row.PayoutAttempt.Should().BeEmpty();
    row.ReconcileAttempts.Should().BeEmpty();
  }

  // THE most important test in this export: withdrawals completed before the
  // payout columns existed (Jul 2026) have no payout row, and that is ~2.5
  // years of the history a tax export exists to produce. The pre-fee Complete
  // transferred the full amount and charged nothing, so those rows must
  // report fee 0.00 and net == gross rather than two empty money columns.
  [Fact]
  public void Legacy_completed_withdrawal_without_payout_exports_zero_fee_and_gross_net()
  {
    var row = MakeWithdrawal(amount: 100m, payout: null, status: WithdrawStatus.Completed)
      .ToExportRow(receiptUrl: null);

    row.GrossAmount.Should().Be("100.00");
    row.Fee.Should().Be("0.00", "the pre-payout Complete transferred gross and charged nothing");
    row.NetAmount.Should().Be("100.00");

    // there genuinely was no gateway payout, so the audit cells stay blank
    row.AirwallexConfirmation.Should().BeEmpty();
    row.PayoutAttempt.Should().BeEmpty();
    row.ReconcileAttempts.Should().BeEmpty();
  }

  // Processing/RequireManualIntervention were appended with the payout
  // feature and Approve always writes a payout first, so a null payout here
  // cannot occur; if it ever did, that money is initiated but not settled, so
  // blank is the honest value — never a 0.00 that implies a free transfer.
  [Theory]
  [InlineData(WithdrawStatus.Processing)]
  [InlineData(WithdrawStatus.RequireManualIntervention)]
  public void In_flight_statuses_without_a_payout_leave_fee_and_net_blank(WithdrawStatus status)
  {
    var row = MakeWithdrawal(payout: null, status: status).ToExportRow(receiptUrl: null);

    row.Fee.Should().BeEmpty();
    row.NetAmount.Should().BeEmpty();
  }

  [Theory]
  [InlineData(WithdrawStatus.Pending)]
  [InlineData(WithdrawStatus.Rejected)]
  [InlineData(WithdrawStatus.Cancel)]
  public void Terminal_no_payment_statuses_blank_fee_and_net_but_retain_payout_audit_fields(
    WithdrawStatus status
  )
  {
    var row = MakeWithdrawal(status: status, payout: Payout(4m)).ToExportRow(receiptUrl: null);

    row.Fee.Should().BeEmpty();
    row.NetAmount.Should().BeEmpty();
    row.AirwallexConfirmation.Should().Be("payout-confirmation");
    row.PayoutAttempt.Should().Be("2");
    row.ReconcileAttempts.Should().Be("1");
  }

  [Fact]
  public void Completion_note_with_csv_special_characters_is_rfc_4180_quoted()
  {
    const string note = "first, \"quoted\" line\nsecond line";
    var row = MakeWithdrawal(complete: Complete(note)).ToExportRow(receiptUrl: null);

    WithdrawalCsv.Line(row).Should().Contain("\"first, \"\"quoted\"\" line\nsecond line\"");
  }

  [Theory]
  [InlineData("=")]
  [InlineData("+")]
  [InlineData("-")]
  [InlineData("@")]
  [InlineData("\t")]
  [InlineData("\r")]
  public void Field_defangs_every_formula_trigger_inside_quotes_when_quoting_is_required(
    string trigger
  )
  {
    var raw = $"{trigger}formula,not-a-formula";

    WithdrawalCsv.Field(raw).Should().Be($"\"'{raw}\"");
  }

  [Fact]
  public void Field_defangs_an_unquoted_formula()
  {
    WithdrawalCsv.Field("=1+1").Should().Be("'=1+1");
  }

  [Fact]
  public void Card_refund_fragment_columns_are_semicolon_joined_and_index_aligned()
  {
    var row = MakeWithdrawal(
        method: WithdrawalMethod.CardRefund,
        refunds:
        [
          new RefundSpec(
            "pi-first",
            "refund-first",
            12.5m,
            RefundFragmentStatus.Settled,
            new DateTime(2024, 2, 1, 2, 3, 4, DateTimeKind.Utc)
          ),
          new RefundSpec("pi-second", null, 0.25m, RefundFragmentStatus.Created, null),
          new RefundSpec(
            "pi-third",
            "refund-third",
            7m,
            RefundFragmentStatus.Failed,
            new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc)
          ),
        ]
      )
      .ToExportRow(receiptUrl: null);

    var paymentIntentIds = row.RefundPaymentIntentIds.Split(WithdrawalCsv.RefundSeparator);
    var refundIds = row.RefundAirwallexIds.Split(WithdrawalCsv.RefundSeparator);
    var amounts = row.RefundAmounts.Split(WithdrawalCsv.RefundSeparator);
    var statuses = row.RefundStatuses.Split(WithdrawalCsv.RefundSeparator);
    var settledAts = row.RefundSettledAts.Split(WithdrawalCsv.RefundSeparator);

    paymentIntentIds.Should().HaveCount(3);
    refundIds.Should().HaveCount(3);
    amounts.Should().HaveCount(3);
    statuses.Should().HaveCount(3);
    settledAts.Should().HaveCount(3);

    var zipped = paymentIntentIds.Select(
      (paymentIntentId, index) =>
        (
          paymentIntentId,
          refundIds[index],
          amounts[index],
          statuses[index],
          settledAts[index]
        )
    );

    zipped.Should().Equal(
      ("pi-first", "refund-first", "12.50", "Settled", "2024-02-01T10:03:04+08:00"),
      ("pi-second", "", "0.25", "Created", ""),
      ("pi-third", "refund-third", "7.00", "Failed", "2024-02-03T12:05:06+08:00")
    );
  }

  // refund_arns is the sixth index-aligned refund_* list, and the ARN is what
  // the accountant traces a card refund by. A settled fragment that has not
  // been backfilled yet must contribute an EMPTY slot rather than being
  // dropped — otherwise every other refund_* column silently shifts by one.
  [Fact]
  public void Refund_arns_are_semicolon_joined_and_index_aligned_with_the_other_refund_columns()
  {
    var row = MakeWithdrawal(
        method: WithdrawalMethod.CardRefund,
        refunds:
        [
          new RefundSpec(
            "pi-first",
            "refund-first",
            12.5m,
            RefundFragmentStatus.Settled,
            new DateTime(2024, 2, 1, 2, 3, 4, DateTimeKind.Utc),
            "12345678901234567890123"
          ),
          // settled at the gateway but the ARN has not been captured yet
          new RefundSpec(
            "pi-second",
            "refund-second",
            0.25m,
            RefundFragmentStatus.Settled,
            new DateTime(2024, 2, 2, 2, 3, 4, DateTimeKind.Utc)
          ),
          // never settled, so the network never issued an ARN
          new RefundSpec("pi-third", null, 7m, RefundFragmentStatus.Created, null),
        ]
      )
      .ToExportRow(receiptUrl: null);

    var arns = row.RefundArns.Split(WithdrawalCsv.RefundSeparator);
    var paymentIntentIds = row.RefundPaymentIntentIds.Split(WithdrawalCsv.RefundSeparator);

    arns.Should().Equal("12345678901234567890123", "", "");
    arns.Should().HaveSameCount(paymentIntentIds, "every refund_* list shares one index space");
    arns.Should().HaveSameCount(row.RefundStatuses.Split(WithdrawalCsv.RefundSeparator));
    arns.Should().HaveSameCount(row.RefundSettledAts.Split(WithdrawalCsv.RefundSeparator));
    arns.Should().HaveSameCount(row.RefundAirwallexIds.Split(WithdrawalCsv.RefundSeparator));
    arns.Should().HaveSameCount(row.RefundAmounts.Split(WithdrawalCsv.RefundSeparator));

    // the alignment is the whole point: slot 0 is the fragment that has one
    paymentIntentIds[0].Should().Be("pi-first");
    arns[0].Should().Be("12345678901234567890123");
  }

  [Fact]
  public void Card_refund_blanks_a_legacy_paynow_number()
  {
    var row = MakeWithdrawal(
        method: WithdrawalMethod.CardRefund,
        payNowNumber: "legacy-number-that-must-not-export"
      )
      .ToExportRow(receiptUrl: null);

    row.PayNowNumber.Should().BeEmpty();
  }

  [Fact]
  public void Paynow_blanks_all_refund_columns_even_when_legacy_fragment_data_exists()
  {
    var row = MakeWithdrawal(
        method: WithdrawalMethod.PayNow,
        refunds:
        [
          new RefundSpec(
            "bad-payment-intent",
            "bad-refund-id",
            99m,
            RefundFragmentStatus.Settled,
            new DateTime(2024, 2, 1, 2, 3, 4, DateTimeKind.Utc)
          ),
        ]
      )
      .ToExportRow(receiptUrl: null);

    new[]
      {
        row.RefundPaymentIntentIds,
        row.RefundAirwallexIds,
        row.RefundAmounts,
        row.RefundStatuses,
        row.RefundSettledAts,
        row.RefundArns,
      }
      .Should()
      .OnlyContain(value => value == "");
  }

  // ARN is a card-network concept: a PayNow payout can never have one, so the
  // column stays blank even if fragment data somehow exists on the row.
  [Fact]
  public void Paynow_blanks_refund_arns_even_when_a_fragment_carries_one()
  {
    var row = MakeWithdrawal(
        method: WithdrawalMethod.PayNow,
        refunds:
        [
          new RefundSpec(
            "bad-payment-intent",
            "bad-refund-id",
            99m,
            RefundFragmentStatus.Settled,
            new DateTime(2024, 2, 1, 2, 3, 4, DateTimeKind.Utc),
            "99999999999999999999999"
          ),
        ]
      )
      .ToExportRow(receiptUrl: null);

    row.RefundArns.Should().BeEmpty();
  }

  [Fact]
  public void Header_is_exactly_the_contract_order()
  {
    WithdrawalCsv.Headers.Should().Equal(ExpectedHeader.Split(','));
    WithdrawalCsv.HeaderLine.Should().Be(ExpectedHeader);
    WithdrawalCsv.Bom.Should().Be("\uFEFF");
  }

  // Column order is the contract with the accountant's importer: append only.
  // refund_arns was added after receipt_key, so it must sit last \u2014 inserting
  // it next to its refund_* siblings would have shifted six existing columns.
  [Fact]
  public void Refund_arns_is_appended_last_behind_receipt_key()
  {
    WithdrawalCsv.Headers.Last().Should().Be("refund_arns");
    WithdrawalCsv.Headers[^2].Should().Be("receipt_key");

    var row = MakeWithdrawal(
        method: WithdrawalMethod.CardRefund,
        refunds:
        [
          new RefundSpec(
            "pi-only",
            "refund-only",
            5m,
            RefundFragmentStatus.Settled,
            new DateTime(2024, 2, 1, 2, 3, 4, DateTimeKind.Utc),
            "12345678901234567890123"
          ),
        ]
      )
      .ToExportRow(receiptUrl: null);

    row.ToFields().Last().Should().Be("12345678901234567890123");
  }

  [Fact]
  public void Every_data_row_has_exactly_one_cell_per_header()
  {
    var row = MakeWithdrawal().ToExportRow(receiptUrl: null);

    row.ToFields()
      .Should()
      .HaveSameCount(
        WithdrawalCsv.Headers,
        "headers and cells are derived from one ordered column table"
      );
  }

  [Fact]
  public void Receipt_key_exports_the_durable_object_key_alongside_the_expiring_link()
  {
    var row = MakeWithdrawal(complete: Complete("done", receipt: "withdrawal/receipt-123.png"))
      .ToExportRow(receiptUrl: "https://minio.test/signed?expires=soon");

    row.ReceiptKey.Should().Be("withdrawal/receipt-123.png");
    row.ReceiptUrl.Should().Be("https://minio.test/signed?expires=soon");

    var receiptKeyIndex = Array.IndexOf(WithdrawalCsv.Headers, "receipt_key");
    row.ToFields()[receiptKeyIndex].Should().Be("withdrawal/receipt-123.png");
  }

  [Fact]
  public void Receipt_key_is_blank_when_no_receipt_was_uploaded()
  {
    MakeWithdrawal(complete: null).ToExportRow(receiptUrl: null).ReceiptKey.Should().BeEmpty();
    MakeWithdrawal(complete: Complete("done"))
      .ToExportRow(receiptUrl: null)
      .ReceiptKey.Should()
      .BeEmpty();
  }

  [Fact]
  public void Amount_uses_fixed_two_decimal_invariant_formatting()
  {
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = new CultureInfo("de-DE");

      WithdrawalCsv.Amount(1234.5m).Should().Be("1234.50");
    }
    finally
    {
      CultureInfo.CurrentCulture = previousCulture;
    }
  }

  [Fact]
  public void Timestamp_treats_unspecified_values_as_utc_and_renders_singapore_offset()
  {
    var utc = new DateTime(2024, 2, 1, 2, 3, 4, DateTimeKind.Utc);
    var unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);

    WithdrawalCsv.Timestamp(utc).Should().Be("2024-02-01T10:03:04+08:00");
    WithdrawalCsv.Timestamp(unspecified).Should().Be("2024-02-01T10:03:04+08:00");
    WithdrawalCsv.Timestamp(utc.ToLocalTime()).Should().Be("2024-02-01T10:03:04+08:00");
    WithdrawalCsv.Timestamp((DateTime?)null).Should().BeEmpty();
  }

  [Fact]
  public void File_name_has_stable_bound_fallbacks()
  {
    WithdrawalCsv.FileName("2024-01-01", "2024-01-31")
      .Should()
      .Be("withdrawals-2024-01-01_2024-01-31.csv");
    WithdrawalCsv.FileName("2024-01-01", null)
      .Should()
      .Be("withdrawals-2024-01-01_latest.csv");
    WithdrawalCsv.FileName(null, null).Should().Be("withdrawals-earliest_latest.csv");
  }

  private static Withdrawal MakeWithdrawal(
    WithdrawalMethod method = WithdrawalMethod.PayNow,
    decimal amount = 100m,
    WithdrawalPayout? payout = null,
    WithdrawStatus status = WithdrawStatus.Completed,
    WithdrawalComplete? complete = null,
    string? payNowNumber = "91234567",
    IReadOnlyList<RefundSpec>? refunds = null
  )
  {
    var withdrawalId = Guid.NewGuid();
    return new Withdrawal
    {
      Principal = new WithdrawalPrincipal
      {
        Id = withdrawalId,
        CreatedAt = new DateTime(2024, 1, 1, 1, 2, 3, DateTimeKind.Utc),
        Status = new WithdrawalStatus { Status = status },
        Record = new WithdrawalRecord
        {
          Amount = amount,
          Method = method,
          PayNowNumber = payNowNumber,
        },
        Complete = complete,
        Payout = payout,
      },
      Wallet = new WalletPrincipal
      {
        Id = Guid.NewGuid(),
        UserId = "user-123",
        Record = new WalletRecord
        {
          Usable = 0m,
          WithdrawReserve = 0m,
          BookingReserve = 0m,
        },
      },
      User = new UserPrincipal
      {
        Id = "user-123",
        Record = new UserRecord { Username = "accountant", Email = "user@example.test" },
      },
      Completer = null,
      Refunds = (refunds ?? []).Select(
        (refund, index) => new WithdrawalRefundFragment
        {
          Id = Guid.NewGuid(),
          WithdrawalId = withdrawalId,
          PaymentId = Guid.NewGuid(),
          PaymentIntentId = refund.PaymentIntentId,
          AirwallexRefundId = refund.AirwallexRefundId,
          RequestId = $"{withdrawalId}-2-{index}",
          Amount = refund.Amount,
          Status = refund.Status,
          AcquirerReferenceNumber = refund.AcquirerReferenceNumber,
          CreatedAt = new DateTime(2024, 1, 1, 1, 2, 3, DateTimeKind.Utc).AddMinutes(index),
          SettledAt = refund.SettledAt,
        }
      ).ToArray(),
    };
  }

  private static WithdrawalPayout Payout(decimal fee) =>
    new()
    {
      ConfirmationNumber = "payout-confirmation",
      Fee = fee,
      Attempt = 2,
      ReconcileAttempts = 1,
    };

  private static WithdrawalComplete Complete(string note, string? receipt = null) =>
    new()
    {
      CompletedAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
      CompleterId = "admin-123",
      Note = note,
      Receipt = receipt,
    };

  private sealed record RefundSpec(
    string PaymentIntentId,
    string? AirwallexRefundId,
    decimal Amount,
    RefundFragmentStatus Status,
    DateTime? SettledAt,
    // null is the common case: unsettled fragments never have one, and
    // history predating ARN capture has not been backfilled yet
    string? AcquirerReferenceNumber = null
  );
}
