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

  // Admin-managed roles for discount/pricing targeting — Roles above mirrors
  // the Descope JWT and is overwritten by the frontend token sync, so
  // admin-granted roles live here and MUST survive that sync (the record
  // Update mapper deliberately never touches this column)
  public string[] ExtraRoles { get; set; } = [];

  // Reference
  public WalletData? Wallet { get; set; }
}
