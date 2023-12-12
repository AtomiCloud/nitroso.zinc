using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace App.Modules.Common;

public record OtelRedisMessage<T>(Dictionary<string, string> Context, T Message);

public class OtelRedisDatabase(IRedisDatabase redis)
{

  private static readonly TextMapPropagator Propagator = new TraceContextPropagator();

  private static readonly JsonSerializerOptions SerializeOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  };

  private static readonly ActivitySource ActivitySource = new("AutoTrace.Redis.Shim");


  public async Task<long> PublishAsync<T>(RedisChannel channel, T message, CommandFlags flag = CommandFlags.None)
  {
    var carrier = new Dictionary<string, string>();

    using var activity = ActivitySource.StartActivity();

    if (activity != null) Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), carrier, (c, k, v) => c[k] = v);

    var otelMessage = new OtelRedisMessage<T>(carrier, message);
    return await redis.PublishAsync(channel, otelMessage, flag);
  }

  public async Task SubscribeAsync<T>(RedisChannel channel, Func<T?, Task> handler,
    CommandFlags flag = CommandFlags.None)
  {
    await redis.SubscribeAsync<OtelRedisMessage<T>>(channel, async m =>
    {
      var carrier = m?.Context ?? [];

      var parentContext = Propagator.Extract(default, carrier,
        (c, k) => c.TryGetValue(k, out var v) ? new[] { v } : Enumerable.Empty<string>());
      Baggage.Current = parentContext.Baggage;

      using var activity = ActivitySource.StartActivity();
      var message = m == null ? default : m.Message;
      await handler(message);
    }, flag);
  }

  public RedisValue StreamAdd<T>(RedisKey key, T message, RedisValue? messageId = null, int? maxLength = null,
    bool useApproximateMaxLength = false, CommandFlags flags = CommandFlags.None)
  {
    var carrier = new Dictionary<string, string>();

    using var activity = ActivitySource.StartActivity();
    if (activity != null)
      Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), carrier, (c, k, v) => c[k] = v);

    var contextJson = JsonSerializer.Serialize(carrier, SerializeOptions);
    var messageJson = JsonSerializer.Serialize(message, SerializeOptions);

    NameValueEntry[] nameValueEntry = [
      new NameValueEntry("context", contextJson),
      new NameValueEntry("message", messageJson),
    ];

    return redis.Database.StreamAdd(key, nameValueEntry,
      messageId, maxLength, useApproximateMaxLength, flags);
  }

  public async Task StreamRead<T>(RedisKey key, RedisValue position, Func<T?, Task> handler, int? count = null,
    CommandFlags flags = CommandFlags.None)
  {
    var entries = redis.Database.StreamRead(key, position, count, flags);
    var messages = entries.Select(x => this.FromNameValueEntry<T>(x.Values));

    foreach (var otelRedisMessage in messages)
    {

      var carrier = otelRedisMessage?.Context ?? [];
      var parentContext = Propagator.Extract(default, carrier,
        (c, k) => c.TryGetValue(k, out var v) ? new[] { v } : Enumerable.Empty<string>());
      Baggage.Current = parentContext.Baggage;

      using var activity = ActivitySource.StartActivity();

      var m = otelRedisMessage == null ? default : otelRedisMessage.Message;
      await handler(m);
    }
  }

  public async Task StreamReadGroup<T>(RedisKey key, RedisValue groupName, RedisValue consumerName,
    Func<T?, Task> handler, RedisValue? position = null, int? count = null, bool noAck = false,
    CommandFlags flags = CommandFlags.None)
  {
    var entries = redis.Database.StreamReadGroup(key, groupName, consumerName, position, count, flags);
    var messages = entries.Select(x => this.FromNameValueEntry<T>(x.Values));

    foreach (var otelRedisMessage in messages)
    {
      var carrier = otelRedisMessage?.Context ?? [];
      var parentContext = Propagator.Extract(default, carrier,
        (c, k) => c.TryGetValue(k, out var v) ? new[] { v } : Enumerable.Empty<string>());
      Baggage.Current = parentContext.Baggage;

      using var activity = ActivitySource.StartActivity();
      var m = otelRedisMessage == null ? default : otelRedisMessage.Message;
      await handler(m);
    }
  }


  private NameValueEntry[] ToNameValueEntry<T>(T message)
  {
    var carrier = new Dictionary<string, string>();
    var contextJson = JsonSerializer.Serialize(carrier, SerializeOptions);
    var messageJson = JsonSerializer.Serialize(message, SerializeOptions);
    NameValueEntry[] nameValueEntry =
    [
      new NameValueEntry("context", contextJson),
      new NameValueEntry("message", messageJson),
    ];
    return nameValueEntry;
  }

  private OtelRedisMessage<T>? FromNameValueEntry<T>(NameValueEntry[] nameValueEntry)
  {
    string? contextJson = nameValueEntry.FirstOrDefault(x => x.Name == "context").Value;
    string? messageJson = nameValueEntry.FirstOrDefault(x => x.Name == "message").Value;
    if (contextJson == null || messageJson == null) return null;

    var context = JsonSerializer.Deserialize<Dictionary<string, string>>(contextJson, SerializeOptions);
    var message = JsonSerializer.Deserialize<T>(messageJson, SerializeOptions);
    if (context == null || message == null) return null;

    return new OtelRedisMessage<T>(context, message);
  }
}
