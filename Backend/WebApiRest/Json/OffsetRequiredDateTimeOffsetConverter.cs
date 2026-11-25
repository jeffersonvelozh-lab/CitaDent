using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebApiRest.Json;

/// <summary>
/// Reads an instant only when it says which offset it is in: "2026-12-24T08:00:00-05:00" or
/// "2026-12-24T13:00:00Z". Without one ("2026-12-24T08:00:00") .NET would take the server's local time zone,
/// which differs between a developer machine and a cloud host, so the request is a 400 instead.
/// Writing is unchanged (ISO 8601 with the offset).
/// </summary>
public sealed class OffsetRequiredDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Both accept only ISO 8601. As a DateTime, a value without an offset has Kind Unspecified.
        if (reader.TokenType != JsonTokenType.String
            || !reader.TryGetDateTimeOffset(out var value)
            || !reader.TryGetDateTime(out var asDateTime)
            || asDateTime.Kind == DateTimeKind.Unspecified)
        {
            throw new JsonException("Expected an ISO 8601 date and time with an offset, such as 2026-12-24T08:00:00-05:00.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }
}
