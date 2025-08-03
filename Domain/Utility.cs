using CSharp_Result;
using Domain.Exceptions;

namespace Domain;

public static class Utility
{
  public static Result<T> NullToError<T>(T? value, string identifier)
    where T : class
  {
    if (value is null)
      return new NotFoundException(typeof(T), identifier);
    return value;
  }

  public static Result<T> NullToError<T>(this Result<T?> value, string identifier)
    where T : class
  {
    return value.Then(x => NullToError(x, identifier));
  }

  public static Task<Result<T>> NullToError<T>(this Task<Result<T?>> value, string identifier)
    where T : class
  {
    return value.Then(x => NullToError(x, identifier));
  }

  public static DateTime ToZonedDateTime(this DateOnly date, TimeOnly time, TimeZoneInfo timezone)
  {
    var dt = date.ToDateTime(time);
    return TimeZoneInfo.ConvertTimeToUtc(dt, timezone);
  }
}
