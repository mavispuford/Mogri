using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace Mogri.Json;

/// <summary>
/// Serializes SKPoint values explicitly so Release trimming cannot interfere with
/// reflection-based property discovery for canvas path coordinates.
/// </summary>
public class SkPointJsonConverter : JsonConverter<SKPoint>
{
    public override SKPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of SKPoint object.");
        }

        float x = 0;
        float y = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new SKPoint(x, y);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected SKPoint property name.");
            }

            var propertyName = reader.GetString();

            if (!reader.Read())
            {
                throw new JsonException("Expected SKPoint property value.");
            }

            if (string.Equals(propertyName, nameof(SKPoint.X), StringComparison.OrdinalIgnoreCase))
            {
                x = reader.GetSingle();
            }
            else if (string.Equals(propertyName, nameof(SKPoint.Y), StringComparison.OrdinalIgnoreCase))
            {
                y = reader.GetSingle();
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of SKPoint JSON payload.");
    }

    public override void Write(Utf8JsonWriter writer, SKPoint value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(SKPoint.X), value.X);
        writer.WriteNumber(nameof(SKPoint.Y), value.Y);
        writer.WriteEndObject();
    }
}