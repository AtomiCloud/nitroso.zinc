using System.ComponentModel.DataAnnotations;
using App.Modules.Users.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Wallets.Data;

public class WalletData
{
  public Guid Id { get; set; }

  [Precision(16, 8)]
  public decimal Usable { get; set; }

  [Precision(16, 8)]
  public decimal WithdrawReserve { get; set; }

  [Precision(16, 8)]
  public decimal BookingReserve { get; set; }

  [MaxLength(128)]
  public string UserId { get; set; } = string.Empty;

  public UserData User { get; set; } = null!;
}
