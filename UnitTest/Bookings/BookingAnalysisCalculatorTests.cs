using Domain.Booking;
using Domain.Timings;
using FluentAssertions;

namespace UnitTest.Bookings;

// The pure summary math behind GET Booking/analysis: totals are sums over
// the per-day rows; priority fees are net of refunds; termination fees are
// the collected cost of terminated bookings minus what was refunded.
public class BookingAnalysisCalculatorTests
{
  private static BookingAnalysisRow Row(
    int tickets,
    decimal gross,
    TrainDirection direction = TrainDirection.JToW
  ) =>
    new()
    {
      Date = new DateOnly(2026, 7, 1),
      Direction = direction,
      Time = new TimeOnly(8, 30),
      TicketsCompleted = tickets,
      GrossRevenue = gross,
    };

  private static readonly DepositSummary NoDeposits = new() { Count = 0, Captured = 0m };

  private static readonly BookingAnalysisLedgerSums ZeroSums = new()
  {
    DepositFees = 0m,
    WithdrawalFees = 0m,
    PriorityFeesCharged = 0m,
    PriorityFeesRefunded = 0m,
    TerminatedGross = 0m,
    TerminationRefunds = 0m,
  };

  [Fact]
  public void Totals_sum_across_rows()
  {
    var rows = new[]
    {
      Row(3, 42m),
      Row(2, 28m, TrainDirection.WToJ),
      Row(5, 80m),
    };

    var s = BookingAnalysisCalculator.Summarize(rows, NoDeposits, ZeroSums);

    s.TotalTickets.Should().Be(10);
    s.TotalGross.Should().Be(150m);
  }

  [Fact]
  public void Empty_rows_produce_zero_totals()
  {
    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, ZeroSums);

    s.TotalTickets.Should().Be(0);
    s.TotalGross.Should().Be(0m);
  }

  [Fact]
  public void Deposits_pass_through_unchanged()
  {
    var deposits = new DepositSummary { Count = 7, Captured = 350.5m };

    var s = BookingAnalysisCalculator.Summarize([], deposits, ZeroSums);

    s.Deposits.Count.Should().Be(7);
    s.Deposits.Captured.Should().Be(350.5m);
  }

  [Fact]
  public void Deposit_and_withdrawal_fees_pass_through()
  {
    var sums = ZeroSums with { DepositFees = 12.5m, WithdrawalFees = 4m };

    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, sums);

    s.InternalFees.Deposit.Should().Be(12.5m);
    s.InternalFees.Withdrawal.Should().Be(4m);
  }

  [Fact]
  public void Priority_fee_is_net_of_refunds()
  {
    // both charge and refund rows share TransactionType.PriorityFee; the
    // net is what BunnyBooker actually kept
    var sums = ZeroSums with { PriorityFeesCharged = 50m, PriorityFeesRefunded = 20m };

    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, sums);

    s.InternalFees.Priority.Should().Be(30m);
  }

  [Fact]
  public void Termination_fee_is_collected_minus_refunded()
  {
    // the BookingTerminated ledger row only carries the refund; the kept fee
    // is the terminated bookings' collected cost minus that refund
    var sums = ZeroSums with { TerminatedGross = 100m, TerminationRefunds = 60m };

    var s = BookingAnalysisCalculator.Summarize([], NoDeposits, sums);

    s.InternalFees.Termination.Should().Be(40m);
  }

  [Fact]
  public void Full_summary_combines_all_parts()
  {
    var rows = new[] { Row(4, 64m) };
    var deposits = new DepositSummary { Count = 2, Captured = 100m };
    var sums = new BookingAnalysisLedgerSums
    {
      DepositFees = 1m,
      WithdrawalFees = 2m,
      PriorityFeesCharged = 30m,
      PriorityFeesRefunded = 10m,
      TerminatedGross = 16m,
      TerminationRefunds = 8m,
    };

    var s = BookingAnalysisCalculator.Summarize(rows, deposits, sums);

    s.TotalTickets.Should().Be(4);
    s.TotalGross.Should().Be(64m);
    s.Deposits.Count.Should().Be(2);
    s.Deposits.Captured.Should().Be(100m);
    s.InternalFees.Deposit.Should().Be(1m);
    s.InternalFees.Withdrawal.Should().Be(2m);
    s.InternalFees.Priority.Should().Be(20m);
    s.InternalFees.Termination.Should().Be(8m);
  }
}
