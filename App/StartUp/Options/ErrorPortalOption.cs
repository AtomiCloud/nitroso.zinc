using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class ErrorPortalOption
{

  public const string Key = "ErrorPortal";

  [Required] public bool Enabled { get; set; } = true;

  [Required] public bool EnableExceptionResponse { get; set; } = false;


  [Required, AllowedValues("http", "https")]
  public string Scheme { get; set; } = string.Empty;

  [Required]
  public string Host { get; set; } = string.Empty;



}
