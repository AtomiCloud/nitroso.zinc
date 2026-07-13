using App.Modules.Discounts.API.V1;
using App.Modules.Passengers.API.V1;
using App.Modules.Timings.API.V1;
using App.Modules.Users.API.V1;
using App.Utility;
using Domain.Booking;
using Domain.Passenger;

namespace App.Modules.Bookings.API.V1;

public static class BookingMapper
{
  // DOMAIN -> RES

  public static string ToRes(this BookStatus status) =>
    status switch
    {
      BookStatus.Pending => "Pending",
      BookStatus.Buying => "Buying",
      BookStatus.Completed => "Completed",
      BookStatus.Cancelled => "Cancelled",
      BookStatus.Refunded => "Refunded",
      BookStatus.Terminated => "Terminated",
      BookStatus.Recovering => "Recovering",
      BookStatus.Duplicate => "Duplicate",
      BookStatus.RequireManualIntervention => "RequireManualIntervention",
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static BookingPassengerRes ToRes(this PassengerRecord p) =>
    new(p.FullName, p.Gender.ToRes(), p.PassportExpiry.ToStandardDateFormat(), p.PassportNumber);

  public static BookingPrincipalRes ToRes(this BookingPrincipal p)
  {
    return new BookingPrincipalRes(
      p.Id,
      p.UserId,
      p.Record.Date.ToStandardDateFormat(),
      p.Record.Time.ToStandardTimeFormat(),
      p.Record.Direction.ToRes(),
      p.Record.Passenger.ToRes(),
      p.CreatedAt,
      p.Status.CompletedAt,
      p.Complete.Ticket,
      p.Complete.TicketNumber,
      p.Complete.BookingNumber,
      p.Status.Status.ToRes(),
      p.Priority,
      p.RecoveryRetries
    );
  }

  public static BookingRes ToRes(this Booking p) => new(p.Principal.ToRes(), p.User.ToRes());

  public static BookingCountRes ToRes(this BookingCount p) =>
    new(
      p.Date.ToStandardDateFormat(),
      p.Time.ToStandardTimeFormat(),
      p.Direction.ToRes(),
      p.TicketsNeeded,
      p.Priority,
      p.Normal
    );

  public static BookingQueuePositionRes ToRes(this BookingQueuePosition p) =>
    new(p.Status.ToRes(), p.Position, p.Total, p.PriorityTotal, p.NormalTotal);

  public static BookingTicketHealthRes ToRes(this BookingTicketHealth h) =>
    new(h.HasRef, h.RefValid);

  public static BookingStatRes ToRes(this BookingStatRow r) =>
    new(
      r.DayOfWeek.ToString(),
      r.Time.ToStandardTimeFormat(),
      r.Direction.ToRes(),
      r.Bucket,
      r.Priority,
      r.DemandBucket,
      r.DeliveryBucket,
      r.Total,
      r.Completed,
      r.Refunded,
      r.Cancelled,
      r.Terminated,
      r.Other
    );

  public static BookingStatsQuery ToDomain(this BookingStatsQueryReq req) =>
    new() { After = req.After?.ToDate(), Before = req.Before?.ToDate() };

  // Sales/revenue analysis
  public static BookingAnalysisQuery ToDomain(this BookingAnalysisQueryReq req) =>
    new() { After = req.After?.ToDate(), Before = req.Before?.ToDate() };

  public static BookingAnalysisRowRes ToRes(this BookingAnalysisRow r) =>
    new(
      r.Date.ToStandardDateFormat(),
      r.Direction.ToRes(),
      r.Time.ToStandardTimeFormat(),
      r.TicketsCompleted,
      r.GrossRevenue,
      r.KtmbCost
    );

  public static DirectionBreakdownRes ToRes(this DirectionBreakdown d) =>
    new(d.Direction.ToRes(), d.Tickets, d.Gross, d.KtmbCost);

  public static MonthlyAnalysisRes ToRes(this MonthlyAnalysisRow m) =>
    new(
      m.Month,
      m.Gross,
      m.KtmbCost,
      m.GatewayPaymentFees,
      m.GatewayPayoutFees,
      m.InternalFees,
      m.Net,
      m.ByDirection.Select(d => d.ToRes())
    );

  public static BookingAnalysisRes ToRes(this BookingAnalysis a) =>
    new(
      a.Rows.Select(r => r.ToRes()),
      new BookingAnalysisSummaryRes(
        a.Summary.TotalTickets,
        a.Summary.TotalGross,
        a.Summary.TotalKtmbCost,
        new DepositSummaryRes(a.Summary.Deposits.Count, a.Summary.Deposits.Captured),
        new InternalFeesRes(
          a.Summary.InternalFees.Deposit,
          a.Summary.InternalFees.Withdrawal,
          a.Summary.InternalFees.Priority,
          a.Summary.InternalFees.Termination
        ),
        new GatewayFeesRes(
          a.Summary.GatewayFees.Payments,
          a.Summary.GatewayFees.Payouts,
          new GatewayFeeCoverageRes(
            a.Summary.GatewayFees.Coverage.PaymentsWithFee,
            a.Summary.GatewayFees.Coverage.PaymentsTotal
          )
        ),
        a.Summary.ByDirection.Select(d => d.ToRes())
      ),
      a.Monthly.Select(m => m.ToRes()),
      a.Components.Select(c => new PriceComponentRes(
        c.Kind,
        c.Name,
        c.TimesApplied,
        c.TotalDelta
      )),
      new ComponentsCoverageRes(a.ComponentsCoverage.WithBreakdown, a.ComponentsCoverage.Total)
    );

  // boost ledger
  public static BookingBoostQuery ToDomain(this BookingBoostQueryReq req) =>
    new()
    {
      After = req.After?.ToDate(),
      Before = req.Before?.ToDate(),
      Limit = req.Limit ?? 50,
      Skip = req.Skip ?? 0,
    };

  public static BookingBoostRes ToRes(this BookingBoost b) =>
    new(
      b.BookingId,
      b.UserId,
      b.UserIdentity,
      b.Date.ToStandardDateFormat(),
      b.Time.ToStandardTimeFormat(),
      b.Direction.ToRes(),
      b.Fee,
      b.Free,
      b.BoostedAt,
      b.GrantedBy
    );

  public static BookingBoostPageRes ToRes(this BookingBoostPage p) =>
    new(p.Total, p.Items.Select(b => b.ToRes()));

  // KTMB ticket cost
  public static KtmbCostChangeRes ToRes(this KtmbCostChange c) =>
    new(c.Id, c.Direction.ToRes(), c.Cost, c.EffectiveAt, c.CreatedAt);

  public static KtmbCostRes ToRes(this KtmbCostView v) =>
    new(
      v.Current.ToDictionary(kv => kv.Key.ToRes(), kv => kv.Value),
      v.Upcoming.Select(c => c.ToRes())
    );

  // REQ -> DOMAIN
  public static BookingCountSearch ToDomain(this BookingCountQuery q) =>
    new() { Date = q.Date.ToDate(), Direction = q.Direction.DirectionToDomain() };

  public static PassengerRecord ToRecord(this BookingPassengerReq req) =>
    new()
    {
      Gender = req.Gender.GenderToDomain(),
      FullName = req.FullName,
      PassportExpiry = req.PassportExpiry.ToDate(),
      PassportNumber = req.PassportNumber,
    };

  public static BookingRecord ToRecord(this CreateBookingReq req) =>
    new()
    {
      Date = req.Date.ToDate(),
      Time = req.Time.ToTime(),
      Direction = req.Direction.DirectionToDomain(),
      Passenger = req.Passenger.ToRecord(),
    };

  public static BookingRecord ToRecord(this UpdateBookingReq req) =>
    new()
    {
      Date = req.Date.ToDate(),
      Time = req.Time.ToTime(),
      Direction = req.Direction.DirectionToDomain(),
      Passenger = req.Passenger.ToRecord(),
    };

  public static BookStatus ToBookStatus(this string status) =>
    status switch
    {
      "Pending" => BookStatus.Pending,
      "Buying" => BookStatus.Buying,
      "Completed" => BookStatus.Completed,
      "Cancelled" => BookStatus.Cancelled,
      "Refunded" => BookStatus.Refunded,
      "Terminated" => BookStatus.Terminated,
      "Recovering" => BookStatus.Recovering,
      "Duplicate" => BookStatus.Duplicate,
      "RequireManualIntervention" => BookStatus.RequireManualIntervention,
      _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

  public static BookingSearch ToDomain(this SearchBookingQuery query) =>
    new()
    {
      Date = query.Date?.ToDate(),
      Time = query.Time?.ToTime(),
      Status = query.Status?.ToBookStatus(),
      Direction = query.Direction?.DirectionToDomain(),
      UserId = query.UserId,
      PassportNumber = query.PassportNumber,
      PassengerName = query.PassengerName,
      // resolved to an absolute cutoff server-side so callers (e.g. the
      // reverter cron) need no clock agreement with zinc
      BuyingBefore =
        query.StuckForMinutes != null
          ? DateTime.UtcNow.AddMinutes(-query.StuckForMinutes.Value)
          : null,
      MissingTicket = query.MissingTicket,
      Sort = query.SortBy?.ToBookingSort(),
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };

  public static BookingSort ToBookingSort(this string sort) =>
    sort switch
    {
      "Timing" => BookingSort.Timing,
      "PassengerName" => BookingSort.PassengerName,
      "PassportNumber" => BookingSort.PassportNumber,
      "BuyTime" => BookingSort.BuyTime,
      "FulfilTime" => BookingSort.FulfilTime,
      _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null),
    };

  // Priority queue (target shapes reuse the Discounts API mapper)
  public static PrioritySettingsRes ToRes(this PrioritySettingsRecord r) =>
    new(
      r.Fee,
      r.AllowAll,
      r.WindowStartSgt?.ToStandardTimeFormat(),
      r.WindowEndSgt?.ToStandardTimeFormat(),
      r.FreeTarget?.ToRes(),
      r.AccessTarget?.ToRes()
    );

  public static PriorityAccessRes ToRes(this PriorityAccess a) => new(a.UserId, a.CreatedAt);

  public static PriorityEligibilityRes ToRes(this PriorityEligibility e) =>
    new(e.Eligible, e.Fee, e.Free);

  public static PrioritySettingsRecord ToDomain(this SetPrioritySettingsReq req) =>
    new()
    {
      Fee = req.Fee,
      AllowAll = req.AllowAll,
      WindowStartSgt = req.WindowStartSgt?.ToTime(),
      WindowEndSgt = req.WindowEndSgt?.ToTime(),
      FreeTarget = req.FreeTarget?.ToDomain(),
      AccessTarget = req.AccessTarget?.ToDomain(),
    };
}
