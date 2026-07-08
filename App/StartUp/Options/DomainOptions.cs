using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class DomainOptions
{
  public const string Key = "Domain";

  [Required]
  public int RefundPercentage { get; set; } = 50;

  // Withdrawal fee, in percent of the requested amount (deducted from the
  // amount before payout), e.g. 4 = 4%
  [Required, Range(0, 100)]
  public decimal WithdrawFeePercentage { get; set; } = 4;

  [Required, Url]
  public string BaseUrl { get; set; } = string.Empty;
  
  [Required, Url]
  public string WhatsAppUrl { get; set; } = string.Empty;
  
  [Required, Url]
  public string TelegramUrl { get; set; } = string.Empty;
  
  [Required, EmailAddress]
  public string SupportEmail { get; set; } = string.Empty;
  
}
