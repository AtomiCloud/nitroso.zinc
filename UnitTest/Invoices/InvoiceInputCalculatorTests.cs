using Domain.Invoice;

namespace UnitTest.Invoices;

// The pure fold behind GET Invoice/inputs: sparse per-source daily rows merge
// into one month, booking-sourced measures split per direction, and the
// termination refund policy is applied in exactly one place.
//
// The June 2026 expectations here are taken from PRODUCTION, cross-checked
// against the two issued June invoices (BB-2026-0601-C / -Z). Where the two
// disagree it is the documented snapshot drift, not a bug — see the
// TerminatedKept test and invoices/PROVENANCE.md.
public class InvoiceInputCalculatorTests
{
  private static readonly DateOnly June = new(2026, 6, 1);

  private static InvoiceInputDailySum Row(
    DateOnly? date = null,
    int direction = 0,
    decimal deposits = 0m,
    decimal paymentMethodFees = 0m,
    decimal gatewayFees = 0m,
    decimal refundFees = 0m,
    int completedCount = 0,
    decimal completedRevenue = 0m,
    int priorityPaidCount = 0,
    decimal priorityFee = 0m,
    int priorityFreeCount = 0,
    int terminatedCount = 0,
    decimal terminatedCollected = 0m,
    int withdrawalCount = 0,
    decimal withdrawalTotal = 0m,
    decimal withdrawalFeeIncome = 0m,
    int withdrawalWithFeeCount = 0
  ) =>
    new()
    {
      Date = date ?? June,
      Direction = direction,
      Deposits = deposits,
      PaymentMethodFees = paymentMethodFees,
      GatewayFees = gatewayFees,
      RefundFees = refundFees,
      CompletedCount = completedCount,
      CompletedRevenue = completedRevenue,
      PriorityPaidCount = priorityPaidCount,
      PriorityFee = priorityFee,
      PriorityFreeCount = priorityFreeCount,
      TerminatedCount = terminatedCount,
      TerminatedCollected = terminatedCollected,
      WithdrawalCount = withdrawalCount,
      WithdrawalTotal = withdrawalTotal,
      WithdrawalFeeIncome = withdrawalFeeIncome,
      WithdrawalWithFeeCount = withdrawalWithFeeCount,
    };

  [Fact]
  public void Gather_EmitsBothRoutes_EvenWhenADirectionSoldNothing()
  {
    // a missing route would silently drop a section from the invoice rather
    // than print a zero line, so both are always emitted in a stable order
    var row = InvoiceInputCalculator.Gather([Row(completedCount: 5, direction: 1)], June);

    row.Routes.Should().HaveCount(2);
    row.Routes[0].Key.Should().Be("jbw");
    row.Routes[0].Direction.Should().Be(InvoiceDirection.JbToWoodlands);
    row.Routes[1].Key.Should().Be("wjb");
    row.Routes[1].Direction.Should().Be(InvoiceDirection.WoodlandsToJb);
    row.Routes[1].Tickets.Should().Be(0);
  }

  [Fact]
  public void Gather_SplitsBookingMeasuresByDirection()
  {
    var row = InvoiceInputCalculator.Gather(
      [
        Row(direction: 1, completedCount: 1544, completedRevenue: 15327m),
        Row(direction: 2, completedCount: 1128, completedRevenue: 11196m),
      ],
      June
    );

    var jbw = row.Routes.Single(r => r.Key == "jbw");
    var wjb = row.Routes.Single(r => r.Key == "wjb");
    jbw.Tickets.Should().Be(1544);
    jbw.Revenue.Should().Be(15327m);
    wjb.Tickets.Should().Be(1128);
    wjb.Revenue.Should().Be(11196m);
  }

  [Fact]
  public void Gather_KeepsHalfOfTerminatedCollected()
  {
    // production June collected 856.00 / 848.00 by direction; the issued
    // invoices printed keptRevenue 428.00 / 424.00 — exactly half
    var row = InvoiceInputCalculator.Gather(
      [
        Row(direction: 1, terminatedCount: 89, terminatedCollected: 856m),
        Row(direction: 2, terminatedCount: 85, terminatedCollected: 848m),
      ],
      June
    );

    var jbw = row.Routes.Single(r => r.Key == "jbw");
    var wjb = row.Routes.Single(r => r.Key == "wjb");
    jbw.Terminated.Count.Should().Be(89);
    jbw.Terminated.KeptRevenue.Should().Be(428m);
    wjb.Terminated.Count.Should().Be(85);
    wjb.Terminated.KeptRevenue.Should().Be(424m);
  }

  [Fact]
  public void Gather_SeparatesPaidAndFreeBoosts()
  {
    // free boosts earn nothing but drive the partner-recovery assumption, so
    // they must never be folded into the paid count
    var row = InvoiceInputCalculator.Gather(
      [Row(direction: 2, priorityPaidCount: 12, priorityFee: 120m, priorityFreeCount: 7)],
      June
    );

    var wjb = row.Routes.Single(r => r.Key == "wjb");
    wjb.Priority.Paid.Should().Be(12);
    wjb.Priority.Fee.Should().Be(120m);
    wjb.Priority.Free.Should().Be(7);
  }

  [Fact]
  public void Gather_SplitsGatewayFeesFromPaymentMethodFees()
  {
    // the invoice prints these as two separate lines: account-level Airwallex
    // billings vs per-payment acceptance fees. Refund fees are excluded from
    // the processing-fee base and reported on their own.
    var row = InvoiceInputCalculator.Gather(
      [Row(paymentMethodFees: 1729.85m, gatewayFees: 739m, refundFees: 18.5m)],
      June
    );

    row.Fees.PaymentMethod.Should().Be(1729.85m);
    row.Fees.Gateway.Should().Be(739m);
    row.RefundFeesExcluded.Should().Be(18.5m);
  }

  [Fact]
  public void Gather_SumsMonthLevelMeasuresAcrossDays()
  {
    var row = InvoiceInputCalculator.Gather(
      [
        Row(date: new DateOnly(2026, 6, 1), deposits: 100m, withdrawalCount: 1,
          withdrawalTotal: 50m, withdrawalFeeIncome: 0.5m, withdrawalWithFeeCount: 1),
        Row(date: new DateOnly(2026, 6, 2), deposits: 250m, withdrawalCount: 2,
          withdrawalTotal: 75m, withdrawalFeeIncome: 1m, withdrawalWithFeeCount: 2),
      ],
      June
    );

    row.GrossDeposits.Should().Be(350m);
    row.Withdrawals.Count.Should().Be(3);
    row.Withdrawals.Total.Should().Be(125m);
    row.Withdrawals.Income.Should().Be(1.5m);
    row.Withdrawals.WithFee.Should().Be(3);
  }

  [Fact]
  public void Gather_ExcludesRowsOutsideTheRequestedMonth()
  {
    // the repository bounds the scan, but the fold must not depend on that —
    // a boundary bug here would silently pull a neighbouring month's revenue
    // into an invoice
    var row = InvoiceInputCalculator.Gather(
      [
        Row(date: new DateOnly(2026, 5, 31), deposits: 999m, direction: 1, completedCount: 99),
        Row(date: new DateOnly(2026, 6, 15), deposits: 100m, direction: 1, completedCount: 10),
        Row(date: new DateOnly(2026, 7, 1), deposits: 999m, direction: 1, completedCount: 99),
      ],
      June
    );

    row.GrossDeposits.Should().Be(100m);
    row.Routes.Single(r => r.Key == "jbw").Tickets.Should().Be(10);
  }

  [Fact]
  public void Gather_LabelsTheMonthInWireFormat()
  {
    InvoiceInputCalculator.Gather([], June).Month.Should().Be("06-2026");
  }

  [Fact]
  public void Gather_OnNoDataReturnsZeroedMonthNotAnError()
  {
    // an unbilled month is a legitimate answer; the caller decides whether a
    // zero month is worth issuing
    var row = InvoiceInputCalculator.Gather([], June);

    row.GrossDeposits.Should().Be(0m);
    row.Routes.Should().HaveCount(2);
    row.Routes.Should().OnlyContain(r => r.Tickets == 0 && r.Revenue == 0m);
    row.Withdrawals.Count.Should().Be(0);
  }
}
