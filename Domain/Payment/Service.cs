using CSharp_Result;
using Domain.Transaction;
using Domain.Wallet;

namespace Domain.Payment;

public class PaymentService(
  IPaymentRepository repo,
  IPaymentGateway gateway,
  IWalletRepository walletRepo,
  ITransactionRepository transactionRepo,
  ITransactionGenerator generator,
  ITransactionManager transactionManager,
  IFeeCalculator feeCalculator
) : IPaymentService
{
  // The full captured amount is deposited first, then the deposit fee (if one
  // is configured — the default is zero) is collected out of Usable into the
  // fee account as its own ledger row. The fee is capped at the amount, so
  // the collect can never overdraw what was just deposited.
  private Task<Result<Payment>> ChargeDepositFee(Payment x)
  {
    return feeCalculator
      .Compute(FeeType.Deposit, x.Principal.Record.CapturedAmount)
      .ThenAwait(async fee =>
      {
        if (fee <= 0)
          return (Result<Payment>)x;
        return await walletRepo
          .Collect(x.Wallet.Id, fee)
          .NullToError(x.Wallet.Id.ToString())
          // no paymentId here: Transactions.PaymentId is UNIQUE and the
          // Deposit ledger row already claims it — linking the fee row too
          // would violate the index and roll back every fee-charging deposit
          .ThenAwait(_ =>
            transactionRepo.Create(x.Wallet.Id, generator.DepositFeeCharge(x.Principal, fee))
          )
          .Then(_ => x, Errors.MapNone);
      });
  }

  public Task<Result<IEnumerable<PaymentPrincipal>>> Search(PaymentSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<IEnumerable<CapturedPayment>>> ListCaptured(CapturedPaymentsQuery query)
  {
    return repo.ListCaptured(query);
  }

  public Task<Result<Payment?>> GetById(Guid id)
  {
    return repo.GetById(id);
  }

  public Task<Result<Payment?>> GetByRef(string id)
  {
    return repo.GetByRef(id);
  }

  public Task<Result<(PaymentPrincipal, PaymentSecret)>> Create(
    Guid walletId,
    decimal amount,
    string currency,
    Guid id
  )
  {
    return gateway
      .Create(id, amount, currency)
      .ThenAwait(x =>
        repo.Create(walletId, x.Item1, x.Item2).Then(p => (p, x.Item3), Errors.MapAll)
      );
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
    return transactionManager.Start(
      () =>
        repo
        // update payment
        .UpdateById(id, record)
          .NullToError(id.ToString())
          // update wallet
          .DoAwait(
            DoType.MapErrors,
            w =>
              walletRepo
                .Deposit(w.Wallet.Id, w.Principal.Record.CapturedAmount)
                .NullToError(w.Wallet.Id.ToString())
          )
          // update transaction
          .DoAwait(
            DoType.MapErrors,
            x =>
              transactionRepo.Create(
                x.Wallet.Id,
                generator.Deposit(x.Principal),
                x.Principal.Reference.Id
              )
          )
          // collect the deposit fee, when one is configured
          .DoAwait(DoType.MapErrors, x => this.ChargeDepositFee(x))
    );
  }

  public Task<Result<Payment>> CompleteByRef(string reference, PaymentRecord record)
  {
    return transactionManager.Start(
      () =>
        repo
        // update payment
        .UpdateByRef(reference, record)
          .NullToError(reference)
          // update wallet
          .DoAwait(
            DoType.MapErrors,
            w =>
              walletRepo
                .Deposit(w.Wallet.Id, w.Principal.Record.CapturedAmount)
                .NullToError(w.Wallet.Id.ToString())
          )
          // update transaction
          .DoAwait(
            DoType.MapErrors,
            x =>
              transactionRepo.Create(
                x.Wallet.Id,
                generator.Deposit(x.Principal),
                x.Principal.Reference.Id
              )
          )
          // collect the deposit fee, when one is configured
          .DoAwait(DoType.MapErrors, x => this.ChargeDepositFee(x))
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
