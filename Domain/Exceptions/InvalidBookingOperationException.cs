using Domain.Booking;

namespace Domain.Exceptions;

public class InvalidBookingOperationException : Exception
{
  public InvalidBookingOperationException(BookStatus bookStatus, string operation)
  {
    this.BookStatus = bookStatus;
    this.Operation = operation;
  }

  public InvalidBookingOperationException(string? message, BookStatus bookStatus, string operation)
    : base(message)
  {
    this.BookStatus = bookStatus;
    this.Operation = operation;
  }

  public InvalidBookingOperationException(
    string? message,
    Exception? innerException,
    BookStatus bookStatus,
    string operation
  )
    : base(message, innerException)
  {
    this.BookStatus = bookStatus;
    this.Operation = operation;
  }

  public BookStatus BookStatus { get; init; }

  public string Operation { get; init; }
}
