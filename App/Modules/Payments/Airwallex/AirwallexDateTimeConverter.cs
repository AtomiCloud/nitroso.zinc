using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace App.Modules.Payments.Airwallex;

// Airwallex writes some timestamps with a colon-less offset
// ("2026-06-22T21:46:40+0000"), which System.Text.Json's built-in DateTime
// converter rejects outright. A rejected date fails the whole response, and
// for the refund listing that fails the refundable-pool read closed — every
// card refund approve / requeue / manual completion on a card with any past
// refund then answers 503. This reader accepts the colon-less offset as well
// as the ISO forms the built-in one handles, and always yields UTC.
public static partial class AirwallexDateTime
{
  [GeneratedRegex(@"([+-])(\d{2})(\d{2})$")]
  private static partial Regex ColonlessOffset();

  public static DateTime? Parse(string? raw)
  {
    if (string.IsNullOrWhiteSpace(raw))
      return null;
    var normalised = ColonlessOffset().Replace(raw.Trim(), "$1$2:$3");
    if (
      DateTimeOffset.TryParse(
        normalised,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
        out var parsed
      )
    )
      return parsed.UtcDateTime;
    throw new JsonException($"Unrecognised Airwallex timestamp '{raw}'");
  }
}

public sealed class AirwallexDateTimeConverter : JsonConverter<DateTime>
{
  public override DateTime Read(
    ref Utf8JsonReader reader,
    Type typeToConvert,
    JsonSerializerOptions options
  ) =>
    AirwallexDateTime.Parse(reader.GetString())
    ?? throw new JsonException("Airwallex timestamp is empty");

  public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
    writer.WriteStringValue(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}

public sealed class AirwallexNullableDateTimeConverter : JsonConverter<DateTime?>
{
  public override bool HandleNull => true;

  public override DateTime? Read(
    ref Utf8JsonReader reader,
    Type typeToConvert,
    JsonSerializerOptions options
  ) => reader.TokenType == JsonTokenType.Null ? null : AirwallexDateTime.Parse(reader.GetString());

  public override void Write(
    Utf8JsonWriter writer,
    DateTime? value,
    JsonSerializerOptions options
  )
  {
    if (value is null)
      writer.WriteNullValue();
    else
      writer.WriteStringValue(
        value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
      );
  }
}
