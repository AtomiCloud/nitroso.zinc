using System.ComponentModel.DataAnnotations;

namespace App.Modules.Bookings.Data;

// One admin "check our KTMB account" request and tin's answer (see
// Domain.Booking.BookingKtmbCheck). Append-only history per booking; the
// newest RequestedAt is what the admin page shows.
public class BookingKtmbCheckData
{
  public Guid Id { get; set; }

  public Guid BookingId { get; set; }

  public BookingData Booking { get; set; } = null!;

  // KtmbCheckStatus
  public byte Status { get; set; }

  public DateTime RequestedAt { get; set; }

  [MaxLength(128)]
  public string? RequestedBy { get; set; }

  public DateTime? CheckedAt { get; set; }

  [MaxLength(128)]
  public string? KtmbBookingNo { get; set; }

  [MaxLength(128)]
  public string? KtmbTicketNo { get; set; }

  public Guid? ClaimedByBookingId { get; set; }

  [MaxLength(1024)]
  public string? Error { get; set; }
}
