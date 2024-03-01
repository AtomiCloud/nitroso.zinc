using System.Security.Cryptography;
using System.Text;
using App.StartUp.Options;
using CSharp_Result;
using Microsoft.Extensions.Options;

namespace App.Modules.Payments.Airwallex;

public class AirwallexHmacCalculator(IOptions<PaymentOption> o)
{
  public Result<string> Compute(string timestamp, string payload)
  {
    var key = o.Value.Airwallex.WebhookKey;
    using var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(key));
    var hash = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(timestamp + payload));
    return Convert.ToHexString(hash).ToLower();
  }
}
