using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Invoice;

namespace UnitTest.Invoices;

// Loads the real invoice data files from invoices/data/ (copied verbatim into
// Fixtures/) and turns them into InvoiceMonthInput.
//
// The DTOs below mirror the JSON EXACTLY rather than binding straight onto the
// domain records. That is deliberate: the JSON is the historical on-disk format
// that produced the issued PDFs, and it must stay pinned even if the domain
// records are later renamed or restructured. If a rename would break this
// mapping, the compiler says so here instead of a fixture silently loading as
// zero.
public static class InvoiceFixture
{
  private static readonly JsonSerializerOptions Json = new()
  {
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
  };

  private static string Path(string name) =>
    System.IO.Path.Combine(AppContext.BaseDirectory, "Invoices", "Fixtures", name);

  public static InvoiceMonthInput Input(string month) =>
    ToDomain(Read<MonthDto>($"input-{month}.json"));

  public static OracleDto Oracle(string month) => Read<OracleDto>($"oracle-{month}.json");

  private static T Read<T>(string name)
  {
    var path = Path(name);
    if (!File.Exists(path))
      throw new FileNotFoundException($"Invoice fixture missing: {path}", path);
    return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json)
           ?? throw new InvalidOperationException($"Invoice fixture is null: {name}");
  }

  private static InvoiceMonthInput ToDomain(MonthDto d) => new()
  {
    Period = new InvoicePeriod
    {
      Label = d.Period!.Label,
      MonthName = d.Period.MonthName,
      Seq = d.Period.Seq,
    },
    IssueDate = d.IssueDate,
    DueDate = d.DueDate,
    Topups = d.Topups.Select(t => new InvoiceTopup { Date = t.Date, Rm = t.Rm, Sgd = t.Sgd }).ToArray(),
    TopupNote = d.TopupNote ?? string.Empty,
    Fees = new InvoiceFeesInput { Gateway = d.Fees!.Gateway, PaymentMethod = d.Fees.PaymentMethod },
    GrossDeposits = d.GrossDeposits,
    RefundFeesExcluded = d.RefundFeesExcluded,
    Routes = d.Routes.Select(rt => new InvoiceRouteInput
    {
      Key = rt.Key,
      Label = rt.Label,
      Short = rt.Short,
      Tickets = rt.Tickets,
      Revenue = rt.Revenue,
      FareRm = rt.FareRm,
      Terminated = new InvoiceTerminatedInput
      {
        Count = rt.Terminated!.Count,
        KeptRevenue = rt.Terminated.KeptRevenue,
        HalfFareSgd = rt.Terminated.HalfFareSgd,
      },
    }).ToArray(),
    Withdrawals = new InvoiceWithdrawalsInput { Count = d.Withdrawals!.Count, Total = d.Withdrawals.Total },
    Infrastructure = d.Infrastructure,
    MarketingSharePct = d.MarketingSharePct,
    Partners = d.Partners.Select(p => new InvoicePartner
    {
      Name = p.Name,
      Suffix = p.Suffix,
      RoundingPreference = p.RoundingPreference == "up"
        ? InvoiceRoundingPreference.Up
        : InvoiceRoundingPreference.Down,
    }).ToArray(),
    Priority = new InvoicePriorityInput
    {
      PerRoute = d.Priority!.PerRoute.ToDictionary(
        kv => kv.Key,
        kv => new InvoicePriorityRouteInput { Paid = kv.Value.Paid, Fee = kv.Value.Fee, Free = kv.Value.Free }),
      KeptOnCancelled = d.Priority.KeptOnCancelled,
      KeptOnCancelledCount = d.Priority.KeptOnCancelledCount,
    },
    Surcharge = new InvoiceSurchargeInput
    {
      Coverage = new InvoiceSurchargeCoverage
      {
        WithBreakdown = d.Surcharge!.Coverage!.WithBreakdown,
        Total = d.Surcharge.Coverage.Total,
      },
      PerRoute = d.Surcharge.PerRoute.ToDictionary(
        kv => kv.Key,
        kv => kv.Value.Lines.Select(l => new InvoicePriceLine
        {
          Kind = l.Kind == "discount" ? InvoicePriceLineKind.Discount : InvoicePriceLineKind.Policy,
          Name = l.Name,
          Count = l.Count,
          Delta = l.Delta,
        }).ToArray()),
    },
    WithdrawalFee = new InvoiceWithdrawalFeeInput
    {
      Income = d.WithdrawalFee!.Income,
      WithFee = d.WithdrawalFee.WithFee,
      Count = d.WithdrawalFee.Count,
    },
    Promotional = new InvoicePromotionalInput { Count = d.Promotional!.Count, Amount = d.Promotional.Amount },
    NetTransfers = d.NetTransfers,
    Duplicates = new InvoiceDuplicatesInput { Count = d.Duplicates!.Count, Refunded = d.Duplicates.Refunded },
    PartnerRecovery = d.PartnerRecovery is null
      ? null
      : new InvoiceRecoveryInput
      {
        FreeBoosts = d.PartnerRecovery.FreeBoosts,
        Tickets = d.PartnerRecovery.Tickets,
        PerBoost = d.PartnerRecovery.PerBoost,
        PerTicket = d.PartnerRecovery.PerTicket,
      },
    FeeRateOverride = d.FeeRateOverride,
    WastedFeeOverride = d.Overrides?.WastedFee,
  };

  // ---- on-disk shapes -----------------------------------------------------

  private record PeriodDto(string Label, string MonthName, string Seq);

  private record TopupDto(string Date, decimal Rm, decimal Sgd);

  private record FeesDto(decimal Gateway, decimal PaymentMethod);

  private record TerminatedDto(int Count, decimal KeptRevenue, decimal HalfFareSgd);

  private record RouteDto(
    string Key, string Label, string Short, int Tickets, decimal Revenue, decimal FareRm,
    TerminatedDto? Terminated);

  private record WithdrawalsDto(int Count, decimal Total);

  private record PartnerDto(string Name, string Suffix, string RoundingPreference);

  private record PriorityRouteDto(int Paid, decimal Fee, int Free);

  private record PriorityDto(
    Dictionary<string, PriorityRouteDto> PerRoute, decimal KeptOnCancelled, int KeptOnCancelledCount)
  {
    public Dictionary<string, PriorityRouteDto> PerRoute { get; init; } = PerRoute ?? [];
  }

  private record CoverageDto(int WithBreakdown, int Total);

  private record PriceLineDto(string Kind, string Name, int Count, decimal Delta);

  private record SurchargeRouteDto(PriceLineDto[] Lines)
  {
    public PriceLineDto[] Lines { get; init; } = Lines ?? [];
  }

  private record SurchargeDto(CoverageDto? Coverage, Dictionary<string, SurchargeRouteDto> PerRoute)
  {
    public Dictionary<string, SurchargeRouteDto> PerRoute { get; init; } = PerRoute ?? [];
  }

  private record WithdrawalFeeDto(decimal Income, int WithFee, int Count);

  private record PromotionalDto(int Count, decimal Amount);

  private record DuplicatesDto(int Count, decimal Refunded);

  private record RecoveryDto(int FreeBoosts, int Tickets, decimal PerBoost, decimal PerTicket);

  private record OverridesDto(decimal? WastedFee);

  private record MonthDto(
    PeriodDto? Period, string IssueDate, string DueDate, TopupDto[] Topups, string? TopupNote,
    FeesDto? Fees, decimal GrossDeposits, decimal RefundFeesExcluded, RouteDto[] Routes,
    WithdrawalsDto? Withdrawals, decimal Infrastructure, decimal MarketingSharePct, PartnerDto[] Partners,
    PriorityDto? Priority, SurchargeDto? Surcharge, WithdrawalFeeDto? WithdrawalFee,
    PromotionalDto? Promotional, decimal NetTransfers, DuplicatesDto? Duplicates,
    RecoveryDto? PartnerRecovery, decimal? FeeRateOverride, OverridesDto? Overrides)
  {
    public TopupDto[] Topups { get; init; } = Topups ?? [];

    public RouteDto[] Routes { get; init; } = Routes ?? [];

    public PartnerDto[] Partners { get; init; } = Partners ?? [];
  }

  // ---- the oracle's output half ------------------------------------------

  public record OracleFx(decimal TotalFundedRm, decimal TotalFundedSgd, decimal FxRate, decimal FxRatePrinted);

  public record OracleFee(decimal TotalPaymentFees, decimal FeeRatePct);

  public record OraclePriority(int Paid, decimal Fee, int Free, decimal Gross, decimal FeeCost, decimal Net);

  public record OracleRoute(
    string Key, decimal TicketFaresRm, decimal TicketFaresSgd, decimal ProcessingFee, decimal DirectCost,
    decimal Contribution, decimal MarginPct, decimal TerminatedNet, OraclePriority? Priority,
    decimal SurchargeGross, decimal DiscountGross);

  public record OracleTotals(
    int Tickets, decimal Revenue, decimal PricePerTicket, decimal TicketFaresRm, decimal TicketFaresSgd,
    decimal ProcessingFee, decimal DirectCost, decimal Contribution, decimal TotalMarginPct);

  public record OracleAdjustments(
    decimal TerminatedNet, int TerminatedCount, decimal WastedFee, decimal WastedFeeComputed,
    decimal Infrastructure);

  public record OracleAncillaryPriority(
    decimal Gross, decimal FeeCost, decimal Net, int Paid, int Free, decimal Kept, int KeptCount);

  public record OracleAncillarySurcharge(decimal Gross, decimal Discounts, decimal CoveragePct);

  public record OracleWithdrawalFee(decimal Income, int WithFee, int Count);

  public record OracleAncillary(
    OracleAncillaryPriority? Priority, OracleAncillarySurcharge? Surcharge,
    OracleWithdrawalFee? WithdrawalFee, decimal Promotional, decimal NetTransfers, decimal Total);

  public record OracleRecovery(
    int FreeBoosts, int Tickets, decimal PerBoost, decimal PerTicket, decimal Boosts,
    decimal TicketsAmount, decimal Total, bool Applies);

  public record OracleShare(
    string Name, string Suffix, decimal Pct, decimal Earned, decimal Advance, decimal Amount);

  public record OracleResult(
    decimal NetProfit, decimal NetMarginPct, decimal ShareBase, decimal MarketingSharePool,
    OracleShare[] Shares)
  {
    public OracleShare[] Shares { get; init; } = Shares ?? [];
  }

  public record OracleDto(
    OracleFx? Fx, OracleFee? Fee, OracleRoute[] Routes, OracleTotals? Totals,
    OracleAdjustments? Adjustments, OracleAncillary? Ancillary, OracleRecovery? Recovery,
    OracleResult? Result)
  {
    public OracleRoute[] Routes { get; init; } = Routes ?? [];
  }
}
