using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Modules.Transactions.Data;
using App.Modules.Users.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

// The per-booking price breakdown captured at purchase (owned JSON, like
// PaymentStatusData): the base cost, one line per applied pricing component
// and the final charged amount. Persisted from this release onward — old
// bookings have a NULL column and are excluded from component analytics,
// never guessed.
public class BookingPriceLineData
{
  // "policy" (signed delta as configured) or "discount" (negative)
  public string Kind { get; set; } = string.Empty;

  public string Name { get; set; } = string.Empty;

  public decimal Delta { get; set; }
}

public class BookingPriceBreakdownData
{
  public decimal BaseCost { get; set; }

  public List<BookingPriceLineData> Lines { get; set; } = [];

  public decimal Final { get; set; }
}

[ComplexType]
public class BookingPassengerData
{
  [MaxLength(512)]
  public required string FullName { get; set; }

  public required byte Gender { get; set; }

  public required DateOnly PassportExpiry { get; set; }

  [MaxLength(64)]
  public required string PassportNumber { get; set; }
}

public class BookingData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // status
  public byte Status { get; set; }

  public DateTime? CompletedAt { get; set; }

  // when the booking last entered Buying (null for rows predating the column)
  public DateTime? LastBuyingAt { get; set; }

  // priority bookings jump to the front of their timeslot's purchase queue
  public bool Priority { get; set; }

  // snapshot of the fee charged when the booking was prioritized; refunded
  // when the booking ends Refunded or Cancelled
  [Precision(16, 8)]
  public decimal? PriorityFee { get; set; }

  // when the booking was prioritized — stamped from the boost-ledger release
  // onward; older boosts have NULL and the ledger falls back to CreatedAt
  public DateTime? PrioritizedAt { get; set; }

  // the caller's sub when someone OTHER than the owner (an admin) invoked
  // prioritize — admin-grant attribution, stamped from the boost-ledger
  // release onward; NULL for self-boosts and all older rows
  [MaxLength(128)]
  public string? PrioritizedBy { get; set; }

  // price breakdown captured at purchase (see BookingPriceBreakdownData);
  // NULL for bookings that predate breakdown persistence
  public BookingPriceBreakdownData? PriceBreakdown { get; set; }

  // how many times this booking was recycled from Recovering back to Pending
  // for another purchase attempt (RecoverRevert); capped by Recovery:MaxRetries
  public int RecoveryRetries { get; set; }

  // record
  public DateOnly Date { get; set; }

  public TimeOnly Time { get; set; }

  public int Direction { get; set; }

  [MaxLength(128)]
  public string? Ticket { get; set; } = null;

  [MaxLength(128)]
  public string? BookingNo { get; set; } = null;

  [MaxLength(128)]
  public string? TicketNo { get; set; } = null;

  // what BunnyBooker ACTUALLY paid KTMB for this ticket, captured by tin at
  // completion or backfilled after the fact; both set together or both NULL
  // (NULL = not recorded — the analysis falls back to the KtmbCosts estimate)
  [Precision(16, 8)]
  public decimal? KtmbAmount { get; set; }

  // ISO currency of KtmbAmount ("MYR" or "SGD")
  [MaxLength(8)]
  public string? KtmbCurrency { get; set; }

  // what KTMB ACTUALLY refunded for this booking when it was terminated,
  // captured by tin's terminator or backfilled after the fact; both set
  // together or both NULL (NULL = not recorded)
  [Precision(16, 8)]
  public decimal? KtmbRefundAmount { get; set; }

  // ISO currency of KtmbRefundAmount (e.g. "MYR")
  [MaxLength(8)]
  public string? KtmbRefundCurrency { get; set; }

  public BookingPassengerData Passenger { get; set; } = null!;

  // FK
  [MaxLength(128)]
  public string UserId { get; set; } = string.Empty;

  public UserData User { get; set; } = null!;

  public Guid TransactionId { get; set; }

  public TransactionData Transaction { get; set; } = null!;
}
