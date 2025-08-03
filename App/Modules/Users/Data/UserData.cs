using System.ComponentModel.DataAnnotations;
using App.Modules.Bookings.Data;
using App.Modules.Wallets.Data;

namespace App.Modules.Users.Data;

public class UserData
{
  // JWT Sub
  [MaxLength(128)]
  public string Id { get; set; } = string.Empty;

  // Custom Username
  [MaxLength(256)]
  public string Username { get; set; } = string.Empty;

  // Optional Email
  [MaxLength(256)]
  public string? Email { get; set; } = null;

  // Optional Email Verified
  public bool? EmailVerified { get; set; } = null;

  // Optional Roles
  public string[]? Roles { get; set; } = null;

  // Reference
  public WalletData? Wallet { get; set; }
}
