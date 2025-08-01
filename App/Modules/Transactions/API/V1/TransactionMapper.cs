using App.Modules.Wallets.API.V1;
using App.Utility;
using Domain.Transaction;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Transactions.API.V1;

public static class TransactionMapper
{
  // RES
  public static string ToRes(this TransactionType type) =>
    type switch
    {
      TransactionType.BookingRequest => TransactionTypes.BookingRequest,
      TransactionType.BookingComplete => TransactionTypes.BookingComplete,
      TransactionType.BookingRefund => TransactionTypes.BookingRefund,
      TransactionType.BookingCancel => TransactionTypes.BookingCancel,
      TransactionType.BookingTerminated => TransactionTypes.BookingTerminated,
      TransactionType.Deposit => TransactionTypes.Deposit,
      TransactionType.WithdrawRequest => TransactionTypes.WithdrawRequest,
      TransactionType.WithdrawComplete => TransactionTypes.WithdrawComplete,
      TransactionType.Promotional => TransactionTypes.Promotional,
      TransactionType.Transfer => TransactionTypes.Transfer,
      TransactionType.WithdrawRejected => TransactionTypes.WithdrawRejected,
      TransactionType.WithdrawCancelled => TransactionTypes.WithdrawCancelled,
      _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

  public static TransactionPrincipalRes ToRes(this TransactionPrincipal transaction) =>
    new(
      transaction.Id,
      transaction.CreatedAt,
      transaction.Record.Name,
      transaction.Record.Description,
      transaction.Record.Type.ToRes(),
      transaction.Record.Amount,
      transaction.Record.From,
      transaction.Record.To
    );

  public static TransactionRes ToRes(this Transaction transaction) =>
    new(transaction.Principal.ToRes(), transaction.Wallet.ToRes());

  // REQ
  public static TransactionType ToTransactionType(this string type) =>
    type switch
    {
      TransactionTypes.BookingRequest => TransactionType.BookingRequest,
      TransactionTypes.BookingComplete => TransactionType.BookingComplete,
      TransactionTypes.BookingRefund => TransactionType.BookingRefund,
      TransactionTypes.BookingCancel => TransactionType.BookingCancel,
      TransactionTypes.BookingTerminated => TransactionType.BookingTerminated,
      TransactionTypes.Deposit => TransactionType.Deposit,
      TransactionTypes.WithdrawRequest => TransactionType.WithdrawRequest,
      TransactionTypes.WithdrawComplete => TransactionType.WithdrawComplete,
      TransactionTypes.Promotional => TransactionType.Promotional,
      TransactionTypes.Transfer => TransactionType.Transfer,
      TransactionTypes.WithdrawRejected => TransactionType.WithdrawRejected,
      TransactionTypes.WithdrawCancelled => TransactionType.WithdrawCancelled,
      _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

  public static TransactionSearch ToDomain(this SearchTransactionQuery query) =>
    new()
    {
      Search = query.Search,
      TransactionType = query.TransactionType?.ToTransactionType(),
      Id = query.Id,
      userId = query.userId,
      WalletId = query.WalletId,
      Before = query.Before?.ToDate(),
      After = query.After?.ToDate(),
      Limit = query.Limit ?? 20,
      Skip = query.Skip ?? 0,
    };
}
