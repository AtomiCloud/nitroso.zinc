using App.Modules.Users.Data;

namespace App.Modules.Passengers.Data;

public class PassengerData
{
  public Guid Id { get; set; }

  public string FullName { get; set; } = string.Empty;

  public int Gender { get; set; } = 0;

  public DateOnly PassportExpiry { get; set; }

  public string PassportNumber { get; set; } = string.Empty;

  // FK
  public string UserId { get; set; } = string.Empty;

  public UserData User { get; set; }
}
