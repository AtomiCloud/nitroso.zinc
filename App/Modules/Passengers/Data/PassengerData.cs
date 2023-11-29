using App.Modules.Users.Data;

namespace App.Modules.Passengers.Data;

public class PassengerData
{
  public Guid Id { get; set; }

  public string FullName { get; set; } = null!;

  public byte Gender { get; set; }

  public DateOnly PassportExpiry { get; set; }

  public string PassportNumber { get; set; } = null!;

  // FK
  public string UserId { get; set; } = null!;

  public UserData User { get; set; } = null!;
}
