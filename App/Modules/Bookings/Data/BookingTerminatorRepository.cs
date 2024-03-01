using App.Modules.Common;
using App.StartUp.Options;
using App.StartUp.Registry;
using App.Utility;
using CSharp_Result;
using Domain.Booking;
using Microsoft.Extensions.Options;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace App.Modules.Bookings.Data;

public class BookingTerminatorRepository(
  IOptions<TerminatorOption> options,
  IRedisClientFactory factory,
  ILogger<BookingTerminatorRepository> logger) : IBookingTerminatorRepository
{
  private IRedisDatabase Redis => factory.GetRedisClient(Caches.Main).Db0;

  public async Task<Result<Unit>> Terminate(BookingTermination termination)
  {
    try
    {
      var otelRedis = new OtelRedisDatabase(this.Redis);
      otelRedis.QueuePush(options.Value.QueueName, termination);
      return await Task.FromResult(new Result<Unit>());
    }
    catch (Exception e)
    {
      logger.LogError(e, "Error terminating booking {@Termination}", termination.ToJson());
      throw;
    }
  }
}
