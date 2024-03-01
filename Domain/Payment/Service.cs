using CSharp_Result;
using Domain.Transaction;

namespace Domain.Payment;

public class PaymentService(
  IPaymentRepository repo,
  IPaymentGateway gateway,
  ITransactionRepository transactionRepo,
  ITransactionGenerator generator,
  ITransactionManager transactionManager
) : IPaymentService
{
  public Task<Result<IEnumerable<PaymentPrincipal>>> Search(PaymentSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<Payment?>> GetById(Guid id)
  {
    return repo.GetById(id);
  }

  public Task<Result<Payment?>> GetByRef(string id)
  {
    return repo.GetByRef(id);
  }


  public Task<Result<(PaymentPrincipal, PaymentSecret)>> Create(Guid walletId, decimal amount, string currency, Guid id)
  {
    return gateway.Create(id, amount, currency)
        .ThenAwait(x => repo.Create(walletId, x.Item1, x.Item2)
          .Then(p => (p, x.Item3), Errors.MapAll)
        )
      ;
  }

  public Task<Result<Payment?>> UpdateById(Guid id, PaymentRecord record)
  {
    return repo.UpdateById(id, record);
  }

  public Task<Result<Payment?>> UpdateByRef(string reference, PaymentRecord record)
  {
    return repo.UpdateByRef(reference, record);
  }

  public Task<Result<Payment>> CompleteById(Guid id, PaymentRecord record)
  {
    return transactionManager.Start(() =>
      repo.UpdateById(id, record)
        .NullToError(id.ToString())
        .DoAwait(DoType.MapErrors, x =>
          transactionRepo.Create(x.Wallet.Id, generator.Deposit(x.Principal))
            .Then(t => repo.Link(t.Id, x.Principal.Reference.Id), Errors.MapAll)
        )
    );
  }

  public Task<Result<Payment>> CompleteByRef(string reference, PaymentRecord record)
  {
    return transactionManager.Start(() =>
      repo.UpdateByRef(reference, record)
        .NullToError(reference)
        .DoAwait(DoType.MapErrors, x =>
          transactionRepo.Create(x.Wallet.Id, generator.Deposit(x.Principal))
            .Then(t => repo.Link(t.Id, x.Principal.Reference.Id), Errors.MapAll)
        )
    );
  }

  public Task<Result<Unit?>> DeleteById(Guid id)
  {
    return repo.DeleteById(id);
  }

  public Task<Result<Unit?>> DeleteByRef(string reference)
  {
    return repo.DeleteByRef(reference);
  }
}
