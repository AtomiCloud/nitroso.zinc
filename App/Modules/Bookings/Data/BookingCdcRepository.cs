using App.Modules.Common;
using App.StartUp.Options;
using App.StartUp.Registry;
using CSharp_Result;
using Domain.Booking;
using Microsoft.Extensions.Options;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace App.Modules.Bookings.Data;

public class BookingCdcRepository(IOptions<CountSyncerOption> options, IRedisClientFactory factory)
  : IBookingCdcRepository
{
  private IRedisDatabase Redis => factory.GetRedisClient(Caches.Stream).Db0;

  public async Task<Result<Unit>> Add(string action)
  {
    var otelRedis = new OtelRedisDatabase(this.Redis);
    var opt = options.Value;
    otelRedis.StreamAdd(
      opt.StreamName,
      new BookingCdcModel("booking", action),
      null,
      (int)opt.StreamLength
    );
    return await Task.FromResult(new Unit().ToResult());
  }
}
