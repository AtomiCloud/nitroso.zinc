using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Modules.Transactions.Data;
using App.Modules.Users.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Bookings.Data;

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

  public BookingPassengerData Passenger { get; set; } = null!;

  // FK
  [MaxLength(128)]
  public string UserId { get; set; } = string.Empty;

  public UserData User { get; set; } = null!;

  public Guid TransactionId { get; set; }

  public TransactionData Transaction { get; set; } = null!;
}
