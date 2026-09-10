using System.Globalization;
using Domain.Invoice;

namespace App.Modules.Invoices.API.V1;

public static class InvoiceMapper
{
  // month-precision wire format; the day is always the 1st
  public const string MonthFormat = "MM-yyyy";

  public static InvoiceInputQuery ToDomain(this InvoiceInputQueryReq req) =>
    new()
    {
      Month = DateOnly.ParseExact(
        req.Month,
        MonthFormat,
        CultureInfo.InvariantCulture,
        DateTimeStyles.None
      ),
    };

  public static InvoiceInputRowRes ToRes(this InvoiceInputRow r) =>
    new(
      r.Month,
      r.GrossDeposits,
      new InvoiceInputFeesRes(r.Fees.Gateway, r.Fees.PaymentMethod),
      r.RefundFeesExcluded,
      r.Routes.Select(route => new InvoiceInputRouteRes(
        route.Key,
        (int)route.Direction,
        route.Tickets,
        route.Revenue,
        new InvoiceInputTerminatedRes(route.Terminated.Count, route.Terminated.KeptRevenue),
        new InvoiceInputPriorityRes(route.Priority.Paid, route.Priority.Fee, route.Priority.Free)
      )),
      new InvoiceInputWithdrawalsRes(
        r.Withdrawals.Count,
        r.Withdrawals.Total,
        r.Withdrawals.Income,
        r.Withdrawals.WithFee
      )
    );
}
