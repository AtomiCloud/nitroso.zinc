using CSharp_Result;

namespace Domain.Payment;

public interface IPaymentService
{
  Task<Result<IEnumerable<PaymentPrincipal>>> Search(PaymentSearch search);

  Task<Result<Payment?>> GetById(Guid id);

  Task<Result<Payment?>> GetByRef(string id);

  Task<Result<(PaymentPrincipal, PaymentSecret)>> Create(
    Guid walletId,
    decimal amount,
    string currency,
    Guid id
  );

  Task<Result<Payment?>> UpdateById(Guid id, PaymentRecord record);

  Task<Result<Payment?>> UpdateByRef(string reference, PaymentRecord record);

  Task<Result<Payment>> CompleteById(Guid id, PaymentRecord record);

  Task<Result<Payment>> CompleteByRef(string reference, PaymentRecord record);

  Task<Result<Unit?>> DeleteById(Guid id);

  Task<Result<Unit?>> DeleteByRef(string reference);
}
