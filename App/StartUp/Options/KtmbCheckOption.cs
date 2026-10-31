using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

// admin "check our KTMB account" requests: the stream-cache list tin's
// recoverer pops (keep equal to tin recoverer.checkQueueName)
public class KtmbCheckOption
{
  public const string Key = "KtmbCheck";

  [Required, MinLength(1)]
  public string QueueName { get; set; } = string.Empty;
}
