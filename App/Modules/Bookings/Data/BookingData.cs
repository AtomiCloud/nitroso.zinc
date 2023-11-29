using System.ComponentModel.DataAnnotations.Schema;
using App.Modules.Users.Data;

namespace App.Modules.Bookings.Data;

public record BookingPassengerData
{
  public required string FullName { get; set; }

  public required byte Gender { get; set; }

  public required DateOnly PassportExpiry { get; set; }

  public required string PassportNumber { get; set; }
}

public class BookingData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // status
  public byte Status { get; set; }

  public DateTime? CompletedAt { get; set; }

  // record
  public DateOnly Date { get; set; }

  public TimeOnly Time { get; set; }

  [Column(TypeName = "jsonb")]
  public BookingPassengerData[] Passengers { get; set; } = null!;


  // FK
  public string UserId { get; set; } = string.Empty;

  public UserData User { get; set; } = null!;
}
