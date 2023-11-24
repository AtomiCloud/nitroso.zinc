using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Trace;

namespace App.StartUp;

public class TraceDecorator<TDecorated> : DispatchProxy
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
  private ActivitySource _activity;
  private TDecorated _decorated;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

  public static TDecorated Create(TDecorated decorated)
  {
    object proxy = Create<TDecorated, TraceDecorator<TDecorated>>()!;
    ((TraceDecorator<TDecorated>)proxy!).SetParameters(decorated);

    return (TDecorated)proxy;
  }

  private void SetParameters(TDecorated decorated)
  {
    this._decorated = decorated;
    this._activity = new ActivitySource($"AutoTrace.{this._decorated?.GetType().FullName ?? "null"}");
  }

  protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
  {
    using var activity =
      this._activity.StartActivity($"{this._decorated?.GetType().FullName}.{targetMethod?.Name ?? "NullMethod"}");
    try
    {
      var result = targetMethod?.Invoke(this._decorated, args);
      return result;
    }
    catch (Exception e)
    {
      activity?.RecordException(e);
      throw;
    }
  }
}
