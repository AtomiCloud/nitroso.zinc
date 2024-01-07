using System.ComponentModel.DataAnnotations;
using App.Modules.Users.Data;

namespace App.Modules.Passengers.Data;

public class PassengerData
{
  public Guid Id { get; set; }

  [MaxLength(512)]
  public string FullName { get; set; } = null!;

  public byte Gender { get; set; }

  public DateOnly PassportExpiry { get; set; }

  [MaxLength(64)]
  public string PassportNumber { get; set; } = null!;

  // FK
  [MaxLength(128)]
  public string UserId { get; set; } = null!;

  public UserData User { get; set; } = null!;
}
