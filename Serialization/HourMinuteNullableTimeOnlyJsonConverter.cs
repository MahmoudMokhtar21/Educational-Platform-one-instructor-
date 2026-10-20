using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Educatinal_Platform.Serialization;

/// <summary>
/// Accepts the native HTML <input type="time"> value (HH:mm), while also
/// accepting the seconds-bearing representation emitted by older API clients.
/// The persisted model continues to use TimeOnly.
/// </summary>
public sealed class HourMinuteNullableTimeOnlyJsonConverter : JsonConverter<TimeOnly?>
{
    private static readonly string[] SupportedFormats = ["HH:mm", "HH:mm:ss"];

    public override TimeOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A time in HH:mm format is required.");
        }

        var value = reader.GetString();
        if (TimeOnly.TryParseExact(value, SupportedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        throw new JsonException("A time in HH:mm format is required.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString("HH:mm", CultureInfo.InvariantCulture));
    }
}
