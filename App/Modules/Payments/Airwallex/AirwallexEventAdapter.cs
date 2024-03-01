using System.Text.Json;
using Domain.Payment;

namespace App.Modules.Payments.Airwallex;

public class AirwallexEventAdapter
{
  public (Guid, PaymentRecord, bool) ProcessEvent(AirwallexEvent evt)
  {
    var id = evt.Data.Object.RequestId;
    var record = new PaymentRecord
    {
      Amount = evt.Data.Object.Amount,
      CapturedAmount = evt.Data.Object.CapturedAmount,
      Currency = evt.Data.Object.Currency,
      LastUpdated = DateTime.UtcNow,
      Status = evt.Data.Object.Status,
      AdditionalData = JsonDocument.Parse("{}"),
    };
    var complete = evt.Data.Object.Status == "SUCCEEDED";
    return (id, record, complete);
  }
}
