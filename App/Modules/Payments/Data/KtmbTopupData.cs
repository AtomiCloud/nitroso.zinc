using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Payments.Data;

// One KTMB card top-up as stored. Amounts carry (16, 8) like every other
// money column here: the gateway reports issuing amounts at 2dp, but the FX
// rate downstream is a ratio of sums, so nothing is truncated on the way in.
public class KtmbTopupData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  [MaxLength(256)]
  public string IssuingTransactionId { get; set; } = string.Empty;

  public DateTime PostedAt { get; set; }

  [Precision(16, 8)]
  public decimal AmountMyr { get; set; }

  [Precision(16, 8)]
  public decimal AmountSgd { get; set; }

  [MaxLength(256)]
  public string MerchantName { get; set; } = string.Empty;

  // KtmbTopupSource: 0 swept from the gateway, 1 entered by an admin
  public byte Source { get; set; }
}
