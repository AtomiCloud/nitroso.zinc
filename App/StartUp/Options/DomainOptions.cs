using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class DomainOptions
{
  public const string Key = "Domain";

  [Required]
  public int RefundPercentage { get; set; } = 50;
  
  [Required, Url]
  public string BaseUrl { get; set; } = string.Empty;
  
  [Required, Url]
  public string WhatsAppUrl { get; set; } = string.Empty;
  
  [Required, Url]
  public string TelegramUrl { get; set; } = string.Empty;
  
  [Required, EmailAddress]
  public string SupportEmail { get; set; } = string.Empty;
  
}
