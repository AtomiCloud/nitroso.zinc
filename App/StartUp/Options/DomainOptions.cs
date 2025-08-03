using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class DomainOptions
{
  public const string Key = "Domain";

  [Required]
  public int RefundPercentage { get; set; } = 50;
}
