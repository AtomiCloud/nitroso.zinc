namespace App.Modules.Invoices.API.V1;

// which SGT month to gather, as "MM-yyyy" (the wire date convention used by
// every other reporting endpoint is dd-MM-yyyy; a month has no day, so this
// is the month-precision form of it)
public record InvoiceInputQueryReq(string Month);

public record InvoiceInputTerminatedRes(int Count, decimal KeptRevenue);

public record InvoiceInputPriorityRes(int Paid, decimal Fee, int Free);

public record InvoiceInputRouteRes(
  string Key,
  int Direction,
  int Tickets,
  decimal Revenue,
  InvoiceInputTerminatedRes Terminated,
  InvoiceInputPriorityRes Priority
);

public record InvoiceInputWithdrawalsRes(int Count, decimal Total, decimal Income, int WithFee);

public record InvoiceInputFeesRes(decimal Gateway, decimal PaymentMethod);

public record InvoiceInputRowRes(
  string Month,
  decimal GrossDeposits,
  InvoiceInputFeesRes Fees,
  decimal RefundFeesExcluded,
  IEnumerable<InvoiceInputRouteRes> Routes,
  InvoiceInputWithdrawalsRes Withdrawals
);
