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

  // Inclusive local-calendar bounds are scanned as a half-open UTC range.
  // DateOnly's endpoints need special treatment: converting 01-01-0001 can
  // underflow UTC, while AddDays(1) on 31-12-9999 overflows before conversion.
  // Use the exact boundary when representable, otherwise saturate at UTC Min/
  // Max so valid API dates never fail before the query runs.
  public static DateTime ToUtcRangeStart(this DateOnly? date, TimeZoneInfo timezone)
  {
    if (date is null)
      return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    try
    {
      return date.Value.ToZonedDateTime(TimeOnly.MinValue, timezone);
    }
    catch (ArgumentException) when (date == DateOnly.MinValue)
    {
      return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    }
  }

  public static DateTime ToUtcRangeEndExclusive(this DateOnly? date, TimeZoneInfo timezone)
  {
    if (date is null)
      return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
    try
    {
      return date == DateOnly.MaxValue
        ? date.Value.ToZonedDateTime(TimeOnly.MaxValue, timezone).AddTicks(1)
        : date.Value.AddDays(1).ToZonedDateTime(TimeOnly.MinValue, timezone);
    }
    catch (ArgumentException) when (date == DateOnly.MaxValue)
    {
      return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
    }
  }
}
