using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class SmtpOption
{
  public const string Key = "Smtp";

  [Required]
  public string Host { get; set; } = string.Empty;

  [Required, Range(1, 65535)]
  public int Port { get; set; } = 587;

  [Required]
  public string Username { get; set; } = string.Empty;

  [Required]
  public string Password { get; set; } = string.Empty;

  [Required, EmailAddress]
  public string FromEmail { get; set; } = string.Empty;

  [Required]
  public string FromName { get; set; } = string.Empty;

  public bool EnableSsl { get; set; } = true;

  public bool UseDefaultCredentials { get; set; } = false;

  public int Timeout { get; set; } = 30000;
}