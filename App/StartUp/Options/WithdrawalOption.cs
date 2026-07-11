using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class WithdrawalOption
{
  public const string Key = "Withdrawal";

  // How far back (days) a captured card payment still counts toward the
  // refundable pool for card-refund withdrawals — mirrors the card networks'
  // refund window
  [Required, Range(1, 3650)]
  public int RefundWindowDays { get; set; } = 180;
}
