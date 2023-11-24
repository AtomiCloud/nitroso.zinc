using System.Text.Json;
using App.Error;
using CSharp_Result;
using Microsoft.Extensions.Options;
using NJsonSchema;

namespace App.Utility;

public static class Utils
{
  public const string StandardDateFormat = "dd-MM-yyyy";

  public static JsonSchema OptionSchema = JsonSchema.CreateAnySchema();

  public static async Task<Result<Unit>> TryFor(this int timeout,
    Func<int, Task<bool>> tryAction,
    Func<Exception> timeoutAction)
  {
    var tries = 0;
    var done = false;
    while (!done)
    {
      if (tries > timeout) return timeoutAction.Invoke();
      done = await tryAction.Invoke(tries);
      await Task.Delay(1000);
      tries++;
    }

    return new Unit();
  }

  public static DateOnly ToDate(this string date)
  {
    return DateOnly.ParseExact(date, StandardDateFormat);
  }

  public static string ToStandardDateFormat(this DateOnly date)
  {
    return date.ToString(StandardDateFormat);
  }


  public static DomainProblemException ToException(this IDomainProblem p)
  {
    return new DomainProblemException(p);
  }

  public static Uri ToUri(this string uri) => new(uri);

  public static string ToJson<T>(this T obj)
  {
    return JsonSerializer.Serialize(obj);
  }

  public static OptionsBuilder<TOptions> RegisterOption<TOptions>(this IServiceCollection service, string key)
    where TOptions : class
  {
    var property = JsonSchema.FromType<TOptions>();
    OptionSchema.Definitions[key] = property;
    OptionSchema.Properties[key] = new JsonSchemaProperty { Reference = property };
    return service.AddOptions<TOptions>()
      .BindConfiguration(key)
      .ValidateDataAnnotations()
      .ValidateOnStart();
  }
}
