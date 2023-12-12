using App.Modules.Common;
using App.StartUp.Options;
using App.StartUp.Registry;
using Microsoft.Extensions.Options;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace App.Modules.Bookings.Data;

public interface IBookingCdcRepository
{
  Task Add(string action);
}

public class BookingCdcRepository(IOptions<CountSyncerOption> options, IRedisClientFactory factory) : IBookingCdcRepository
{
  private IRedisDatabase Redis => factory.GetRedisClient(Caches.Main).Db0;

  public Task Add(string action)
  {
    var otelRedis = new OtelRedisDatabase(this.Redis);
    var opt = options.Value;
    otelRedis.StreamAdd(opt.StreamName,
      new BookingCdcModel("booking", action), null,
      (int)opt.StreamLength);
    return Task.CompletedTask;
  }
}
