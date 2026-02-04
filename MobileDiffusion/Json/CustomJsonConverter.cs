using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MobileDiffusion.Json;

/// <summary>
///     Created to handle deserializing to a Dictionary in a better way.
///     Json.Net deserializes dictionaries of string, object to JArray etc when the value contains an array.
/// </summary>
public class CustomJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(object);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader == null) return null;

        switch (reader.TokenType)
        {
            case JsonToken.StartArray:
                // JArrays to lists
                return JToken.Load(reader).ToObject<List<object>>();
            case JsonToken.StartObject:
                // Dictionaries to dictionaries with lists
                var token = JToken.Load(reader);
                var loaded = token.ToObject<Dictionary<string, object?>>();

                if (loaded == null) return null;

                var dictionary = new Dictionary<string, object?>();

                foreach (var item in loaded)
                {
                    if (item.Value is JArray jArray)
                    {
                        var jTokenType = jArray.First?.Type;

                        if (jTokenType == null)
                        {
                            dictionary.Add(item.Key, jArray.ToObject<List<object>>());
                            continue;
                        }

                        if (jTokenType is JTokenType.String)
                        {
                            dictionary.Add(item.Key, jArray.ToObject<List<string>>());
                        }
                        else if (jTokenType is JTokenType.Integer)
                        {
                            dictionary.Add(item.Key, jArray.ToObject<List<long>>());
                        }
                        else if (jTokenType is JTokenType.Boolean)
                        {
                            dictionary.Add(item.Key, jArray.ToObject<List<bool>>());
                        }
                        else if (jTokenType is JTokenType.Float)
                        {
                            dictionary.Add(item.Key, jArray.ToObject<List<double>>());
                        }
                        else
                        {
                            dictionary.Add(item.Key, jArray.ToObject<List<object>>());
                        }
                    }
                    else
                    {
                        dictionary.Add(item.Key, item.Value);
                    }
                }

                return dictionary;
            default:
                if (reader.ValueType == null && reader.TokenType != JsonToken.Null)
                    throw new NotImplementedException(nameof(CustomJsonConverter));
                return reader.Value;
        }
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
