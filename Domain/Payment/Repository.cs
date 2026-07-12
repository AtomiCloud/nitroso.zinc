using CSharp_Result;

namespace Domain.Payment;

public interface IPaymentRepository
{
  Task<Result<IEnumerable<PaymentPrincipal>>> Search(PaymentSearch search);

  // intents that captured money in the range, newest first — evidence rows
  // for the analysis page's deposit figures
  Task<Result<IEnumerable<CapturedPayment>>> ListCaptured(CapturedPaymentsQuery query);

  Task<Result<Payment?>> GetById(Guid id);

  Task<Result<Payment?>> GetByRef(string id);

  Task<Result<PaymentPrincipal>> Create(Guid walletId, PaymentReference r, PaymentRecord record);

  Task<Result<Payment?>> UpdateById(Guid id, PaymentRecord record);

  Task<Result<Payment?>> UpdateByRef(string reference, PaymentRecord record);

  Task<Result<Unit?>> DeleteById(Guid id);

  Task<Result<Unit?>> DeleteByRef(string reference);
}
