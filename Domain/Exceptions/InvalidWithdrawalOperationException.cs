using Domain.Withdrawal;

namespace Domain.Exceptions;

public class InvalidWithdrawalOperationException : Exception
{
  public InvalidWithdrawalOperationException(WithdrawStatus withdrawStatus, string operation)
  {
    this.WithdrawStatus = withdrawStatus;
    this.Operation = operation;
  }

  public InvalidWithdrawalOperationException(
    string? message,
    WithdrawStatus withdrawStatus,
    string operation
  )
    : base(message)
  {
    this.WithdrawStatus = withdrawStatus;
    this.Operation = operation;
  }

  public InvalidWithdrawalOperationException(
    string? message,
    Exception? innerException,
    WithdrawStatus withdrawStatus,
    string operation
  )
    : base(message, innerException)
  {
    this.WithdrawStatus = withdrawStatus;
    this.Operation = operation;
  }

  public WithdrawStatus WithdrawStatus { get; init; }

  public string Operation { get; init; }
}
