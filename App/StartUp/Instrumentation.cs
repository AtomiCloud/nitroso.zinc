using System.Diagnostics.Metrics;
using App.StartUp.Options;
using Microsoft.Extensions.Options;

namespace App.StartUp;

public class Instrumentation : IDisposable
{
  private readonly Meter _meter;

  public Instrumentation(
    IOptionsMonitor<AppOption> app,
    string meterName)
  {
    var a = app.CurrentValue;
    this._meter = new Meter(meterName, a.Version);
    this.UserRequests = this._meter.CreateCounter<long>("user.requests", "Number of user requests");
  }

  public Counter<long> UserRequests { get; }

  public void Dispose()
  {
    this._meter.Dispose();
  }
}
