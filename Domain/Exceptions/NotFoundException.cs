namespace Domain.Exceptions;

public class NotFoundException : Exception
{
  public NotFoundException(Type type, string requestIdentifier)
  {
    this.Type = type;
    this.RequestIdentifier = requestIdentifier;
  }

  public NotFoundException(string? message, Type type, string requestIdentifier)
    : base(message)
  {
    this.Type = type;
    this.RequestIdentifier = requestIdentifier;
  }

  public NotFoundException(
    string? message,
    Exception? innerException,
    Type type,
    string requestIdentifier
  )
    : base(message, innerException)
  {
    this.Type = type;
    this.RequestIdentifier = requestIdentifier;
  }

  public Type Type { get; init; }
  public string RequestIdentifier { get; init; }
}
