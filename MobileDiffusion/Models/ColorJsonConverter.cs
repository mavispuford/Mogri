using System.Text.Json;
using System.Text.Json.Serialization;

namespace MobileDiffusion.Models;

/// <summary>
///     Converts <see cref="Color"/> To hex and back for JSON serialization.
/// </summary>
public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => Color.FromArgb(reader.GetString());

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToHex());
}
