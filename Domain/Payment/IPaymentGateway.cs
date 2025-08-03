using CSharp_Result;

namespace Domain.Payment;

public interface IPaymentGateway
{
  // domain Id
  public Task<Result<(PaymentReference, PaymentRecord, PaymentSecret)>> Create(
    Guid id,
    decimal amount,
    string currency
  );
}
